
#include "sfcube2.hpp"
#include "filebuf.hpp"
#include "fp32vec8.hpp"
#include "pbr_lut.hpp"
#include "gl/glcontext.hpp"

#include <thread>

const float SFCubeMapFilter::defaultRoughnessTable[7] =
{
  0.00000000f, 0.10435608f, 0.21922359f, 0.34861218f, 0.50000000f, 0.69098301f,
  1.00000000f
};

void SFCubeMapFilter::processImage_Specular(
    unsigned char *outBufP, int w, int y0, int y1, float roughness)
{
  size_t  startPos = size_t(y0) * size_t(w);
  size_t  endPos = size_t(y1) * size_t(w);
  int     w2 = std::max< int >(w, filterMinWidth);
  std::uint8_t  l2w = std::uint8_t(std::bit_width((unsigned int) w) - 1);
  size_t  cubeFilterTableSize = (size_t(w2) * size_t(w2) * 30) >> 3;
  std::vector< FloatVector4 > tmpBuf(endPos - startPos, FloatVector4(0.0f));
  float   a = roughness * roughness;
  float   a2 = a * a;
  for (size_t j0 = 0; (j0 + 10) <= cubeFilterTableSize; )
  {
    // accumulate convolution output in tmpBuf using smaller partitions of the
    // impulse response so that data used from cubeCoordTable and inBuf fits in
    // L1 cache
    size_t  j1 = j0 + 400;
    j1 = std::min(j1, cubeFilterTableSize);
    FloatVector4  *tmpBufPtr = tmpBuf.data();
    for (size_t i = startPos; i < endPos; i++, tmpBufPtr++)
    {
      int     x = int(i & (size_t(w) - 1));
      int     y = int((i >> l2w) & (size_t(w) - 1));
      int     n = int(i >> (l2w + l2w));
      // v1 = reflected view vector (R), assume V = N = R
      FloatVector4  v1(convertCoord(x, y, w, n));
      FloatVector8  v1x(v1[0]);
      FloatVector8  v1y(v1[1]);
      FloatVector8  v1z(v1[2]);
      FloatVector8  c_r(0.0f);
      FloatVector8  c_g(0.0f);
      FloatVector8  c_b(0.0f);
      FloatVector8  totalWeight(0.0f);
      FloatVector8  a2m1(a2 - 1.0f);
      FloatVector8  a2p1(a2 + 1.0f);
      const FloatVector8  *j =
          reinterpret_cast< const FloatVector8 * >(cubeFilterTable) + j0;
      const FloatVector8  *endPtr = j + (j1 - j0);
      for ( ; (j + 10) <= endPtr; j += 10)
      {
        // v2 = light vector
        FloatVector8  v2x(j[0]);
        FloatVector8  v2y(j[1]);
        FloatVector8  v2z(j[2]);
        // d = N·L = R·L = 2.0 * N·H * N·H - 1.0
        FloatVector8  lDotR = (v1x * v2x) + (v1y * v2y) + (v1z * v2z);
        std::uint32_t signMask = lDotR.getSignMask();
        FloatVector8  v2w(j[3]);
        // D denominator = (N·H * N·H * (a2 - 1.0) + 1.0)² * 4.0
        //               = ((R·L + 1.0) * (a2 - 1.0) + 2.0)²
        FloatVector8  d(lDotR);
        d = d.absValues() * a2m1 + a2p1;
        d = v2w / (d * d);
        if (signMask != 255U)
        {
          FloatVector8  weight(lDotR);  // face +X, +Y or +Z
          weight.maxValues(FloatVector8(0.0f));
          weight *= d;
          c_r += (j[4] * weight);
          c_g += (j[5] * weight);
          c_b += (j[6] * weight);
          totalWeight += weight;
          if (signMask == 0U) [[likely]]
            continue;
        }
        {
          FloatVector8  weight(lDotR);  // face -X, -Y or -Z: invert dot product
          weight.minValues(FloatVector8(0.0f));
          weight *= d;
          c_r -= (j[7] * weight);
          c_g -= (j[8] * weight);
          c_b -= (j[9] * weight);
          totalWeight -= weight;
        }
      }
      FloatVector4  c(c_r.dotProduct(FloatVector8(1.0f)),
                      c_g.dotProduct(FloatVector8(1.0f)),
                      c_b.dotProduct(FloatVector8(1.0f)),
                      totalWeight.dotProduct(FloatVector8(1.0f)));
      *tmpBufPtr = *tmpBufPtr + c;
    }
    j0 = j1;
  }

  FloatVector4  *tmpBufPtr = tmpBuf.data();
  size_t  m = (size_t(1) << (l2w + l2w)) - 1;
  size_t  f = faceDataSize;
  for (size_t i = startPos; (i + 2) <= endPos; tmpBufPtr = tmpBufPtr + 2, i++)
  {
    FloatVector8  c(tmpBufPtr);
    FloatVector8  d(c);
    c = c * normalizeScale / d.shuffleValues(0xFF);
    FloatVector4  tmp[2];
    c.convertToFloatVector4(tmp);
    size_t  n = i >> (l2w + l2w);
    unsigned char *p = outBufP + (n * f);
    pixelStoreFunction(p + ((i & m) * sizeof(std::uint32_t)), tmp[0]);
    i++;
    n = i >> (l2w + l2w);
    p = outBufP + (n * f);
    pixelStoreFunction(p + ((i & m) * sizeof(std::uint32_t)), tmp[1]);
  }
}

void SFCubeMapFilter::processImage_ImportanceSample(
    unsigned char *outBufP, int w, int y0, int y1, float scale)
{
  const FloatVector4  *importanceSampleData = importanceSampleTable->data();
  size_t  sCnt = importanceSampleTable->size();
  std::uint8_t  l2w = std::uint8_t(std::bit_width((unsigned int) w) - 1);
  for (int y = y0; y < y1; y++)
  {
    int     n = y >> l2w;
    int     yc = y & (w - 1);
    unsigned char *p =
        outBufP + (size_t(n) * faceDataSize
                   + (size_t(yc) * size_t(w) * sizeof(std::uint32_t)));
    for (int x = 0; x < w; x++, p = p + sizeof(std::uint32_t))
    {
      FloatVector4  normal(convertCoord(x, yc, w, n));
      FloatVector4  t_z(1.0f, 0.0f, 0.0f, 0.0f);
      if (float(std::fabs(normal[2])) < 0.999f)
        t_z = FloatVector4(0.0f, 0.0f, 1.0f, 0.0f);
      FloatVector4  t_x(t_z.crossProduct3(normal));
      t_x /= float(std::sqrt(t_x.dotProduct3(t_x)));
      FloatVector4  t_y(normal.crossProduct3(t_x));
      t_y /= float(std::sqrt(t_y.dotProduct3(t_y)));
      FloatVector4  c =
          cubeMap->cubeMapImportanceSample(t_x, t_y, normal,
                                           importanceSampleData, sCnt);
      pixelStoreFunction(p, c * scale);
    }
  }
}

void SFCubeMapFilter::processImage_Copy(
    unsigned char *outBufP, int w, int y0, int y1)
{
  int     mipLevel = int(std::bit_width((unsigned int) cubeMap->getWidth()))
                     - int(std::bit_width((unsigned int) w));
  mipLevel = std::max< int >(mipLevel - 1, 0);
  std::uint8_t  l2w = std::uint8_t(std::bit_width((unsigned int) w) - 1);
  for (int y = y0; y < y1; y++)
  {
    int     n = y >> l2w;
    int     yc = y & (w - 1);
    unsigned char *p =
        outBufP + (size_t(n) * faceDataSize
                   + (size_t(yc) * size_t(w) * sizeof(std::uint32_t)));
    for (int x = 0; x < w; x++, p = p + sizeof(std::uint32_t))
    {
      FloatVector4  v(convertCoord(x, yc, w, n));
      FloatVector4  c(cubeMap->cubeMap(v[0], v[1], v[2], float(mipLevel)));
      pixelStoreFunction(p, c * normalizeScale);
    }
  }
}

void SFCubeMapFilter::pixelStore_R8G8B8A8(unsigned char *p, FloatVector4 c)
{
  std::uint32_t tmp = std::uint32_t(c.srgbCompress()) | 0xFF000000U;
  FileBuffer::writeUInt32Fast(p, tmp);
}

void SFCubeMapFilter::pixelStore_R9G9B9E5(unsigned char *p, FloatVector4 c)
{
  FileBuffer::writeUInt32Fast(p, c.convertToR9G9B9E5());
}

void SFCubeMapFilter::threadFunction(
    SFCubeMapFilter *p, unsigned char *outBufP,
    int w, int y0, int y1, float filterParam, bool enableFilter)
{
  if (!enableFilter)
    p->processImage_Copy(outBufP, w, y0, y1);
  else if (!p->importanceSampleTable)
    p->processImage_Specular(outBufP, w, y0, y1, filterParam);
  else
    p->processImage_ImportanceSample(outBufP, w, y0, y1, filterParam);
}

void SFCubeMapFilter::createFilterTable(int w)
{
  int     mipLevel = int(std::bit_width((unsigned int) cubeMap->getWidth()))
                     - int(std::bit_width((unsigned int) w));
  mipLevel = std::max< int >(mipLevel - 1, 0);
  for (int n = 0; n < 6; n = n + 2)
  {
    for (int y = 0; y < w; y++)
    {
      for (int x = 0; x < w; x++)
      {
        FloatVector4  v(convertCoord(x, y, w, n));
        FloatVector4  c(cubeMap->cubeMap(v[0], v[1], v[2], float(mipLevel)));
        FloatVector4  c2(cubeMap->cubeMap(-(v[0]), -(v[1]), -(v[2]),
                                          float(mipLevel)));
        float   *p = cubeFilterTable;
        // reorder data for more efficient use of SIMD
        p = p + ((((n >> 1) * w + y) * w + (x & ~7)) * 10 + (x & 7));
        p[0] = v[0];
        p[8] = v[1];
        p[16] = v[2];
        p[24] = v[3];
        p[32] = c[0];
        p[40] = c[1];
        p[48] = c[2];
        p[56] = c2[0];
        p[64] = c2[1];
        p[72] = c2[2];
      }
    }
  }
}

SFCubeMapFilter::SFCubeMapFilter(size_t outputWidth)
{
  setOutputWidth(outputWidth);
  roughnessTable = &(defaultRoughnessTable[0]);
  roughnessTableSize = int(sizeof(defaultRoughnessTable) / sizeof(float));
  normalizeLevel = float(12.5 / 6.0);
  importanceSampleCnt = 0xFFFFFFFFU;
}

SFCubeMapFilter::~SFCubeMapFilter()
{
}

size_t SFCubeMapFilter::convertImage(
    unsigned char *buf, size_t bufSize, bool outFmtFloat, size_t bufCapacity)
{
  if (bufSize < 148)
    return 0;
  faceDataSize = 0;
  for (std::uint32_t w2 = width * width; w2; w2 = w2 >> 2)
    faceDataSize += w2;
  faceDataSize = faceDataSize * sizeof(std::uint32_t);
  size_t  newSize = faceDataSize * 6 + 148;
  if (std::max(bufSize, bufCapacity) < newSize)
    return 0;
  try
  {
    DDSTexture16  t(buf, bufSize, -1);
    cubeMap = &t;
    if (!(t.getIsCubeMap() && t.getWidth() >= minWidth &&
          t.getWidth() == t.getHeight()))
    {
      return 0;
    }
    normalizeScale = 1.0f;
    // DXGI_FORMAT_R16G16B16A16_FLOAT,
    // DXGI_FORMAT_BC6H_UF16 or DXGI_FORMAT_BC6H_SF16: normalize float formats
    if (t.getDXGIFormat() == 0x0A ||
        t.getDXGIFormat() == 0x5F || t.getDXGIFormat() == 0x60)
    {
      FloatVector4  txSum(0.0f);
      for (int n = 0; n < 6; n++)
        txSum += FloatVector4::convertFloat16(cubeMap->getPixelN(0, 0, 16, n));
      float   tmp =
          txSum.dotProduct3(FloatVector4(0.2126f, 0.7152f, 0.0722f, 1.0f))
          * normalizeLevel;
      if (tmp > 1.0f)
        normalizeScale = 1.0f / std::min(tmp, 65536.0f);
    }

    int     mipCnt = int(std::bit_width(width));
    if (!outFmtFloat)
    {
      pixelStoreFunction = &pixelStore_R8G8B8A8;
      // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB
      FileBuffer::writeDDSHeader(buf, 0x1D, int(width), int(width),
                                 mipCnt, true);
    }
    else
    {
      pixelStoreFunction = &pixelStore_R9G9B9E5;
      // DXGI_FORMAT_R9G9B9E5_SHAREDEXP
      FileBuffer::writeDDSHeader(buf, 0x43, int(width), int(width),
                                 mipCnt, true);
    }
    unsigned char *outBufP = buf + 148;

    std::vector< FloatVector4 > importanceSampleBuf;
    std::vector< FloatVector8 > cubeFilterTableBuf;
    cubeFilterTable = nullptr;

    int     threadCnt = int(std::thread::hardware_concurrency());
    threadCnt = std::min< int >(std::max< int >(threadCnt, 1), 24);
    std::thread *threads[24];
    for (int i = 0; i < 24; i++)
      threads[i] = nullptr;
    int     w = int(width);
    for (int m = 0; w > 0; m++, w = w >> 1)
    {
      int     w2 = std::max< int >(w, filterMinWidth);
      float   roughness = 1.0f;
      if (m < roughnessTableSize)
        roughness = roughnessTable[m];
      bool    enableFilter = (roughness >= (3.0f / 128.0f));
      if (w >= filterMinWidth)
        cubeFilterTable = nullptr;
      importanceSampleTable = nullptr;
      std::uint32_t importanceSampleLimit = std::uint32_t(m + 16 - mipCnt) << 1;
#if ENABLE_X86_64_SIMD >= 4
      importanceSampleLimit = 44739243U >> importanceSampleLimit;
#elif ENABLE_X86_64_SIMD == 3
      importanceSampleLimit = 50331648U >> importanceSampleLimit;
#elif ENABLE_X86_64_SIMD == 2
      importanceSampleLimit = 33554432U >> importanceSampleLimit;
#else
      importanceSampleLimit = 134217728U >> importanceSampleLimit;
#endif
      float   filterParam = roughness;
      if (enableFilter && importanceSampleCnt < importanceSampleLimit)
      {
        int     n = int(importanceSampleCnt);
        importanceSampleTable = &importanceSampleBuf;
        importanceSampleBuf.resize(size_t(n));
        float   a = roughness * roughness;
        float   a2 = a * a;
        // ensure nDotH > 0.7071 for all samples
        float   thetaScale = float(n) / ((float(n) - 0.75f) * (a2 + 1.0f));
        thetaScale = std::min(thetaScale, 1.0f);
        float   mipScale = float(t.getWidth()) * float(t.getWidth())
                           * thetaScale / (float(n) * a2);
        filterParam = 0.0f;
        for (int i = 0; i < n; i++)
        {
          FloatVector4  h(SF_PBR_Tables::Hammersley(i, n));
          h[1] = h[1] * thetaScale;
          h = SF_PBR_Tables::importanceSampleGGX(h, a2);
          float   nDotH = h[2];
          FloatVector4  l(h);
          // L = reflect(-N, H)
          l = l * (nDotH * 2.0f) - FloatVector4(0.0f, 0.0f, 1.0f, 0.0f);
          l[2] = std::max(l[2], 0.0f);
          float   *bufp = &(importanceSampleBuf.data()[i & ~3][i & 3]);
          bufp[0] = l[0];
          bufp[4] = l[1];
          bufp[8] = l[2];
          filterParam += l[2];
          // calculate mip level, based on formula from
          // https://chetanjags.wordpress.com/2015/08/26/image-based-lighting/
          float   d = nDotH * nDotH * (a2 - 1.0f) + 1.0f;
          float   mipLevel =            // mip bias = +1.0
              float(std::log2(d * d * mipScale)) * 0.5f + 2.29248125f;
          bufp[12] = std::min(std::max(mipLevel, 0.0f), 16.0f);
        }
        filterParam = normalizeScale / filterParam;
      }
      else if (enableFilter && !cubeFilterTable)
      {
        cubeFilterTableBuf.resize((size_t(w2) * size_t(w2) * 30) >> 3,
                                  FloatVector8(0.0f));
        cubeFilterTable =
            reinterpret_cast< float * >(cubeFilterTableBuf.data());
        createFilterTable(w2);
      }
      try
      {
        threadCnt = std::min< int >(threadCnt, std::max< int >(w >> 3, 1));
        int     y0 = 0;
        for (int i = 0; i < threadCnt; i++)
        {
          int     y1 = (w * 6 * (i + 1)) / threadCnt;
          threads[i] = new std::thread(threadFunction, this, outBufP, w,
                                       y0, y1, filterParam, enableFilter);
          y0 = y1;
        }
        for (int i = 0; i < threadCnt; i++)
        {
          threads[i]->join();
          delete threads[i];
          threads[i] = nullptr;
        }
      }
      catch (...)
      {
        for (int i = 0; i < 24; i++)
        {
          if (threads[i])
          {
            threads[i]->join();
            delete threads[i];
          }
        }
        throw;
      }
      outBufP = outBufP + (size_t(w * w) * sizeof(std::uint32_t));
    }
    return newSize;
  }
  catch (NifSkopeError&)
  {
  }
  return 0;
}

void SFCubeMapFilter::setRoughnessTable(const float *p, size_t n)
{
  if (!p)
  {
    p = &(defaultRoughnessTable[0]);
    n = std::min< size_t >(n, sizeof(defaultRoughnessTable) / sizeof(float));
  }
  roughnessTable = p;
  roughnessTableSize = int(n);
}

SFCubeMapCache::SFCubeMapCache()
  : SFCubeMapFilter(256)
{
}

SFCubeMapCache::~SFCubeMapCache()
{
}

size_t SFCubeMapCache::convertImage(
    unsigned char *buf, size_t bufSize, bool outFmtFloat, size_t bufCapacity,
    int hdrToneMap)
{
  bool    isHDR = (bufSize >= 11 &&                     // "#?RADIAN"
                   FileBuffer::readUInt64Fast(buf) == 0x4E41494441523F23ULL);
  std::uint64_t k = width | (importanceSampleCnt << 16);
  if (isHDR)
  {
    hdrToneMap = std::clamp< int >(hdrToneMap, 0, 16);
    k = k | (std::uint64_t(hdrToneMap) << 32);
  }
  {
    NifSkopeOpenGLContext::ShapeDataHash  tmp(0, 0, bufSize, nullptr, buf);
    k = k ^ tmp.h[0];
  }
  std::vector< unsigned char >& v = cachedTextures[k];
  if (v.size() > 0)
  {
    std::memcpy(buf, v.data(), v.size());
    return v.size();
  }
  size_t  newSize = 0;
  if (!isHDR)
  {
    newSize =
        SFCubeMapFilter::convertImage(buf, bufSize, outFmtFloat, bufCapacity);
  }
  else
  {
    std::vector< unsigned char >  tmpBuf;
    float   maxLevel = float(hdrToneMap > 0 ? (-65536 >> hdrToneMap) : 65504);
    if (convertHDRToDDS(tmpBuf, buf, bufSize, 2048, false, maxLevel, 0x0A))
    {
      newSize = SFCubeMapFilter::convertImage(tmpBuf.data(), tmpBuf.size(),
                                              outFmtFloat, tmpBuf.size());
      if (newSize && newSize <= std::max(bufSize, bufCapacity))
        std::memcpy(buf, tmpBuf.data(), newSize);
      else
        newSize = 0;
    }
  }
  if (newSize)
  {
    v.resize(newSize);
    std::memcpy(v.data(), buf, newSize);
    return newSize;
  }
  return bufSize;
}

#if ENABLE_GCC_SIMD_32
using FloatVecType = FloatVector8;
#else
using FloatVecType = FloatVector4;
#endif

static inline FloatVecType atan2NormFast(
    FloatVecType y, FloatVecType x, bool xNonNegative = false)
{
  // assumes x² + y² = 1.0, returns atan2(y, x) / π
  FloatVecType  xAbs(x);
  FloatVecType  yAbs(y);
  if (!xNonNegative)
    xAbs.absValues();
  yAbs.absValues();
  FloatVecType  tmp(xAbs);
  tmp.minValues(yAbs);
  FloatVecType  tmp2(tmp * tmp);
  FloatVecType  tmp3(tmp2 * tmp);
  tmp = (((tmp3 * 0.39603792f) + (tmp2 * -0.98507216f) + (tmp * 1.09851059f)
          - 0.65754361f) * tmp3
         + (tmp2 * 0.26000725f) + (tmp * -0.05051132f) + 0.05919720f) * tmp3
        + (tmp2 * -0.00037558f) + (tmp * 0.31831810f);
  tmp.blendValues(FloatVecType(0.5f) - tmp, xAbs - yAbs);
  if (!xNonNegative)
    tmp.blendValues(FloatVecType(1.0f) - tmp, x);
  tmp.blendValues(tmp * -1.0f, y);
  return tmp;
}

void SFCubeMapCache::convertHDRToDDSThread(
    unsigned char *outBuf, size_t outPixelSize, int cubeWidth,
    int yStart, int yEnd,
    const FloatVector4 *hdrTexture, int w, int h, float maxLevel)
{
  unsigned char *p =
      outBuf + (size_t(yStart) * size_t(cubeWidth) * outPixelSize);
  constexpr size_t  k = sizeof(FloatVecType) / sizeof(float);
  for ( ; yStart < yEnd; yStart++)
  {
    int     n = yStart / cubeWidth;
    int     y = yStart % cubeWidth;
    std::int32_t  xi_v[k];
    std::int32_t  yi_v[k];
    FloatVecType  xf_v(0.0f);
    FloatVecType  yf_v(0.0f);
    for (int x = 0; x < cubeWidth; x++, p = p + outPixelSize)
    {
      int     j = x & int(k - 1);
      // convert to spherical coordinates
      if (!j)
      {
#if ENABLE_GCC_SIMD_32
        FloatVecType  tmpX(0.5f, 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f, 7.5f);
#else
        FloatVecType  tmpX(0.5f, 1.5f, 2.5f, 3.5f);
#endif
        FloatVecType  z(float(cubeWidth >> 1));
        tmpX += FloatVecType(float(x)) - z;
        FloatVecType  tmpY(z - (float(y) + 0.5f));
        if (n < 2)
        {
          std::swap(tmpX, z);
          if (n == 0)                   // +X
            z *= -1.0f;
          else                          // -X
            tmpX *= -1.0f;
        }
        else if (n < 4)
        {
          std::swap(tmpY, z);
          if (n == 2)                   // +Y
            z *= -1.0f;
          else                          // -Y
            tmpY *= -1.0f;
        }
        else if (n == 5)                // -Z
        {
          tmpX *= -1.0f;
          z *= -1.0f;
        }
        FloatVecType  tmp(tmpX * tmpX);
        tmp += tmpY * tmpY;
        tmp += z * z;
        tmp = FloatVecType(1.0f) / tmp.squareRoot();
        tmpX *= tmp;
        tmpY *= tmp;
        z *= tmp;
        FloatVecType  xy = ((tmpX * tmpX) + (tmpY * tmpY)).squareRoot();
        tmpX /= xy;
        tmpY /= xy;
        FloatVecType  yf = atan2NormFast(z, xy, true) + 0.5f;
        FloatVecType  xf = atan2NormFast(tmpX, tmpY) * 0.5f + 0.5f;
        xf = xf * float(w) - 0.5f;
        yf = yf * float(h) - 0.5f;
        FloatVecType  xi = FloatVecType(xf).floorValues();
        FloatVecType  yi = FloatVecType(yf).floorValues();
        xi.convertToInt32(xi_v);
        yi.convertToInt32(yi_v);
        xf_v = xf - xi;
        yf_v = yf - yi;
      }
      int     x0 = xi_v[j];
      int     y0 = yi_v[j];
      float   xf = xf_v[j];
      float   yf = yf_v[j];
      x0 = (x0 <= (w - 1) ? (x0 >= 0 ? x0 : (w - 1)) : 0);
      int     x1 = (x0 < (w - 1) ? (x0 + 1) : 0);
      int     y1 = std::clamp< int >(y0 + 1, 0, h - 1);
      y0 = std::clamp< int >(y0, 0, h - 1);
      // bilinear interpolation
      const FloatVector4  *inPtr = hdrTexture + (y0 * w);
      FloatVector4  c(inPtr[x0] * (1.0f - xf) + (inPtr[x1] * xf));
      inPtr = inPtr + (y1 > y0 ? w : 0);
      c = c + (((inPtr[x0] * (1.0f - xf) + (inPtr[x1] * xf)) - c) * yf);
      c.maxValues(FloatVector4(0.0f));
      if (maxLevel < 0.0f)
        c = c * FloatVector4(maxLevel) / (FloatVector4(maxLevel) - c);
      else
        c.minValues(FloatVector4(maxLevel));
      c[3] = 1.0f;
      if (outPixelSize == sizeof(std::uint64_t))
        FileBuffer::writeUInt64Fast(p, c.convertToFloat16());
      else
        FileBuffer::writeUInt32Fast(p, c.convertToR9G9B9E5());
    }
  }
}

bool SFCubeMapCache::convertHDRToDDS(
    std::vector< unsigned char >& outBuf,
    const unsigned char *inBufData, size_t inBufSize,
    int cubeWidth, bool invertCoord, float maxLevel, unsigned char outFmt)
{
  // file should begin with "#?RADIANCE\n"
  if (inBufSize < 11 ||
      FileBuffer::readUInt64Fast(inBufData) != 0x4E41494441523F23ULL ||
      FileBuffer::readUInt32Fast(inBufData + 7) != 0x0A45434EU)
  {
    return false;
  }
  FileBuffer  inBuf(inBufData, inBufSize, 11);
  const char  *lineBuf = reinterpret_cast< const char * >(inBuf.getReadPtr());
  int     w = 0;
  int     h = 0;
  while (inBuf.getPosition() < inBuf.size())
  {
    unsigned char c = inBuf.readUInt8Fast();
    if (c < 0x08)
      break;
    if (c != 0x0A)
      continue;
    if ((lineBuf[0] == '-' || lineBuf[0] == '+') && lineBuf[1] == 'Y')
    {
      if (lineBuf[0] == '+')
        invertCoord = !invertCoord;
      const char  *s = lineBuf + 2;
      while (*s == '\t' || *s == ' ')
        s++;
      long    n = 0;
      for ( ; *s >= '0' && *s <= '9'; s++)
        n = n * 10L + long(*s - '0');
      if (n < 8 || n > 32768)
        break;
      h = int(n);
      while (*s == '\t' || *s == ' ')
        s++;
      if (!(s[0] == '+' && s[1] == 'X'))
        break;
      s = s + 2;
      while (*s == '\t' || *s == ' ')
        s++;
      n = 0;
      for ( ; *s >= '0' && *s <= '9'; s++)
        n = n * 10L + long(*s - '0');
      if ((*s == '\t' || *s == '\n' || *s == '\r' || *s == ' ') &&
          n >= 8 && n <= 32768)
      {
        w = int(n);
      }
      break;
    }
    lineBuf = reinterpret_cast< const char * >(inBuf.getReadPtr());
  }
  if (!w || !h)
    return false;
  std::vector< std::uint32_t >  tmpBuf(size_t(w * h), 0U);
  for (int y = 0; y < h; y++)
  {
    if ((inBuf.getPosition() + 4ULL) > inBuf.size())
      return false;
    std::uint32_t *p =
        tmpBuf.data() + size_t((invertCoord ? y : ((h - 1) - y)) * w);
    std::uint32_t tmp =
        FileBuffer::readUInt32Fast(inBuf.data() + inBuf.getPosition());
    if (tmp != ((std::uint32_t(w & 0xFF) << 24) | (std::uint32_t(w >> 8) << 16)
                | 0x0202U))
    {
      // old RLE format
      unsigned char lenShift = 0;
      for (int x = 0; x < w; )
      {
        if ((inBuf.getPosition() + 4ULL) > inBuf.size())
          return false;
        std::uint32_t c = inBuf.readUInt32Fast();
        if ((c & 0x00FFFFFFU) != 0x00010101U || x < 1)
        {
          lenShift = 0;
          p[x] = c;
          x++;
        }
        else
        {
          size_t  l = (c >> 24) << lenShift;
          lenShift = 8;
          for ( ; l; l--, x++)
          {
            if (x >= w)
              return false;
            p[x] = p[x - 1];
          }
        }
      }
    }
    else
    {
      // new RLE format
      inBuf.setPosition(inBuf.getPosition() + 4);
      for (unsigned char c = 0; c < 32; c = c + 8)
      {
        for (int x = 0; x < w; )
        {
          if (inBuf.getPosition() >= inBuf.size())
            return false;
          unsigned char l = inBuf.readUInt8Fast();
          if (l <= 0x80)
          {
            // copy literals
            for ( ; l; l--, x++)
            {
              if (x >= w || inBuf.getPosition() >= inBuf.size())
                return false;
              p[x] |= (std::uint32_t(inBuf.readUInt8Fast()) << c);
            }
          }
          else
          {
            // RLE
            if (inBuf.getPosition() >= inBuf.size())
              return false;
            std::uint32_t b = std::uint32_t(inBuf.readUInt8Fast()) << c;
            for ( ; l > 0x80; l--, x++)
            {
              if (x >= w)
                return false;
              p[x] |= b;
            }
          }
        }
      }
    }
  }
  std::vector< FloatVector4 > tmpBuf2(tmpBuf.size(), FloatVector4(0.0f));
  for (size_t i = 0; i < tmpBuf.size(); i++)
  {
    std::uint32_t b = tmpBuf[i];
    FloatVector4  c(b);
    int     e = int(b >> 24);
#if defined(__i386__) || defined(__x86_64__) || defined(__x86_64)
    e = std::clamp< int >(e, 16, 240) - 9;
    c *= std::bit_cast< float >(std::uint32_t(e << 23));
#else
    e = std::clamp< int >(e, 103, 165) - 103;
    c *= float(std::int64_t(1) << e) * float(0.5 / (65536.0 * 65536.0));
#endif
    c[3] = 1.0f;
    tmpBuf2[i] = c;
  }
  size_t  outPixelSize =        // 8 bytes for DXGI_FORMAT_R16G16B16A16_FLOAT
      (outFmt == 0x0A ? sizeof(std::uint64_t) : sizeof(std::uint32_t));
  outBuf.resize(size_t(cubeWidth * cubeWidth) * 6 * outPixelSize + 148, 0);
  unsigned char *p = outBuf.data();
  (void) FileBuffer::writeDDSHeader(p, outFmt, cubeWidth, cubeWidth, 1, true);
  p = p + 148;

  int     threadCnt = int(std::thread::hardware_concurrency());
  threadCnt = std::min< int >(threadCnt, std::min< int >(cubeWidth >> 3, 24));
  threadCnt = std::max< int >(threadCnt, 1);
  std::thread *threads[24];
  for (int i = 0; i < 24; i++)
    threads[i] = nullptr;
  try
  {
    int     y0 = 0;
    for (int i = 0; i < threadCnt; i++)
    {
      int     y1 = (cubeWidth * 6 * (i + 1)) / threadCnt;
      threads[i] = new std::thread(convertHDRToDDSThread, p, outPixelSize,
                                   cubeWidth, y0, y1, tmpBuf2.data(), w, h,
                                   maxLevel);
      y0 = y1;
    }
    for (int i = 0; i < threadCnt; i++)
    {
      threads[i]->join();
      delete threads[i];
      threads[i] = nullptr;
    }
  }
  catch (...)
  {
    for (int i = 0; i < 24; i++)
    {
      if (threads[i])
      {
        threads[i]->join();
        delete threads[i];
      }
    }
    throw;
  }
  return true;
}

void SFCubeMapCache::clear()
{
  cachedTextures.clear();
}

