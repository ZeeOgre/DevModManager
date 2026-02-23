
#include "fp32vec4.hpp"
#include "common.cpp"
#include "filebuf.cpp"

static std::vector< YMM_Double >  cieColorMatchTable;

static double cieGaussianFunc(double l, double u, double t1, double t2)
{
  double  tmp = (l - u) * (l < u ? t1 : t2);
  return std::exp(tmp * tmp * -0.5);
}

static YMM_Double cieColorTemp(double t)
{
  int     n = 4096;
  if (cieColorMatchTable.size() < (size_t(n) + 1)) [[unlikely]]
  {
    cieColorMatchTable.resize(size_t(n) + 1);
    YMM_Double  xyzWeight = { 0.0, 0.0, 0.0, 0.0 };
    for (int i = 0; i < n; i++)
    {
      double  l = 300.0 + (500.0 * double(i) / double(n - 1));
      double  x = (1.056 * cieGaussianFunc(l, 599.8, 0.0264, 0.0323))
                  + (0.362 * cieGaussianFunc(l, 442.0, 0.0624, 0.0374))
                  - (0.065 * cieGaussianFunc(l, 501.1, 0.0490, 0.0382));
      double  y = (0.821 * cieGaussianFunc(l, 568.8, 0.0213, 0.0247))
                  + (0.286 * cieGaussianFunc(l, 530.9, 0.0613, 0.0322));
      double  z = (1.217 * cieGaussianFunc(l, 437.0, 0.0845, 0.0278))
                  + (0.681 * cieGaussianFunc(l, 459.0, 0.0385, 0.0725));
      YMM_Double  tmp = { x, y, z, l };
      xyzWeight += tmp;
      cieColorMatchTable[i] = tmp;
    }
    cieColorMatchTable[n] = xyzWeight;
  }
  YMM_Double  xyzSum = { 0.0, 0.0, 0.0, 0.0 };
  double  pi = std::atan(1.0) * 4.0;
  double  h = 6.62607015e-34;
  double  c = 299792458.0;
  double  k = 1.380649e-23;
  double  c1 = pi * 2.0 * h * c * c;
  double  c2 = h * c / k;
  for (int i = 0; i < n; i++)
  {
    YMM_Double  xyz = cieColorMatchTable[i];
    double  l = xyz[3] * 0.000000001;
    double  m = (c1 / (l * l * l * l * l)) / (std::exp(c2 / (l * t)) - 1.0);
    xyzSum += (xyz * m);
  }
  xyzSum /= cieColorMatchTable[n];
  double  x = xyzSum[0];
  double  y = xyzSum[1];
  double  z = xyzSum[2];
  double  r = ((x * 3.2406) + (y * -1.5372) + (z * -0.4986));
  double  g = ((x * -0.9689) + (y * 1.8758) + (z * 0.0415));
  double  b = ((x * 0.0557) + (y * -0.2040) + (z * 1.0570));
  YMM_Double  tmp = { r, g, b, 1.0 };
  return tmp;
}

// flags & 1:   multiply error near x0
// flags & 2:   ignore error for y < 0, minimize maximum error
// flags & 4:   multiply error near x1
// flags & 8:   high multiplier for bits 0 to 2 (100 instead of 10)
// flags & 16:  monotonically decreasing function if bit 5 is set
// flags & 32:  require monotonic function

static double calculateError(const double *f, int n, const double *a, int k,
                             double x0, double x1, double maxErr,
                             unsigned int flags)
{
  double  errSum = 0.0;
  double  errMax = 0.0;
  double  err = 0.0;
  double  prv = (!(flags & 0x10) ? -1000000.0 : 1000000.0);
  for (int i = 0; i <= n; i++)
  {
    double  x = (double(i) / double(n)) * (x1 - x0) + x0;
    double  y = f[i];
    double  z = 0.0;
    switch (k)
    {
      case 8:
        z = z * x + a[8];
        [[fallthrough]];
      case 7:
        z = z * x + a[7];
        [[fallthrough]];
      case 6:
        z = z * x + a[6];
        [[fallthrough]];
      case 5:
        z = z * x + a[5];
        [[fallthrough]];
      case 4:
        z = z * x + a[4];
        [[fallthrough]];
      case 3:
        z = z * x + a[3];
        [[fallthrough]];
      case 2:
        z = z * x + a[2];
        [[fallthrough]];
      case 1:
        z = z * x + a[1];
        [[fallthrough]];
      case 0:
        z = z * x + a[0];
        break;
    }
    double  tmpErr;
    if (!(flags & 2))
      tmpErr = (z - y) * (z - y);
    else
      tmpErr = std::fabs(std::max(z, 0.0) - std::max(y, 0.0));
    if (((flags & 0x09) == 0x01 && i < (n >> 3)) ||
        ((flags & 0x0C) == 0x04 && (n - i) < (n >> 3)))
    {
      tmpErr *= 10.0;
    }
    if (((flags & 0x09) == 0x09 && i < (n >> 6)) ||
        ((flags & 0x0C) == 0x0C && (n - i) < (n >> 6)))
    {
      tmpErr *= 100.0;
    }
    errSum += tmpErr;
    errMax = std::max(errMax, tmpErr);
    if (!(flags & 0x02))
      err += tmpErr;
    else
      err = errSum + (errMax * double(n + 1));
    if (err > maxErr)
      return 1000000000.0;
    if (((flags & 0x30) == 0x20 && z < prv) ||
        ((flags & 0x30) == 0x30 && z > prv))
    {
      err += 1.0;
    }
    prv = z;
  }
  return err;
}

static void solveEquationSystem(double *m, int w, int h)
{
  // Gaussian elimination
  for (int i = 0; i < h; i++)
  {
    double  a = m[i * w + i];
    int     l = i;
    for (int j = i + 1; j < h; j++)
    {
      if (std::fabs(m[j * w + i]) > std::fabs(a))
      {
        a = m[j * w + i];
        l = j;
      }
    }
    if (l != i)
    {
      for (int j = 0; j < w; j++)
      {
        double  tmp = m[i * w + j];
        m[i * w + j] = m[l * w + j];
        m[l * w + j] = tmp;
      }
    }
    for (int j = 0; j < w; j++)
      m[i * w + j] = m[i * w + j] / a;
    m[i * w + i] = 1.0;
    for (int j = i + 1; j < h; j++)
    {
      a = m[j * w + i];
      for (int k = 0; k < w; k++)
        m[j * w + k] = m[j * w + k] - (m[i * w + k] * a);
      m[j * w + i] = 0.0;
    }
  }
  for (int i = h; --i >= 0; )
  {
    for (int j = i - 1; j >= 0; j--)
    {
      double  a = m[j * w + i];
      for (int k = 0; k < w; k++)
        m[j * w + k] = m[j * w + k] - (m[i * w + k] * a);
      m[j * w + i] = 0.0;
    }
  }
}

static double interpolateFunction(const double *f, int n, int k, double *m,
                                  double x)
{
  int     n0 = int(std::floor(x - double(k >> 1)));
  n0 = (n0 > 0 ? n0 : 0);
  int     n1 = n0 + k;
  n1 = (n1 < n ? n1 : n);
  n0 = n1 - k;
  x = x - double(n0);
  for (int i = 0; i <= k; i++)
  {
    m[i * (k + 2)] = 1.0;
    for (int j = 1; j <= k; j++)
      m[i * (k + 2) + j] = m[i * (k + 2) + (j - 1)] * double(i);
    m[i * (k + 2) + (k + 1)] = f[n0 + i];
  }
  solveEquationSystem(m, k + 2, k + 1);
  double  y = 0.0;
  for (int i = k; i >= 0; i--)
    y = y * x + m[i * (k + 2) + (k + 1)];
  return y;
}

static double getRandom(double minVal, double maxVal)
{
  static unsigned int seed = 0x7FFFFFFEU;
  seed = (unsigned int) ((seed * 16807ULL) % 0x7FFFFFFFU);
  double  x = double(int(seed) - 1) / double(0x7FFFFFFD);
  return ((maxVal - minVal) * x + minVal);
}

// flags & 1:   multiply error near x0
// flags & 2:   ignore error for y < 0, minimize maximum error
// flags & 4:   multiply error near x1
// flags & 8:   high multiplier for bits 0 to 2 (100 instead of 10)
// flags & 16:  monotonically decreasing function if bit 5 is set
// flags & 32:  require monotonic function
// flags & 64:  require exact match at x0
// flags & 128: require exact match at (x0 + x1) / 2
// flags & 256: require exact match at x1

static void findPolynomial(double *a, int k, const double *f, int n,
                           double x0, double x1, unsigned int flags,
                           size_t maxIterations = 0x7FFFFFFF)
{
  if (k > n)
    return;
  std::vector< double > m(size_t((k + 2) * (k + 1)), 0.0);
  std::vector< double > p(size_t(k + 1));
  std::vector< double > bestP(size_t(k + 1), double(n >> 1));
  std::vector< double > y(size_t(k + 1));
  std::vector< double > aTmp(size_t(k + 1), 0.0);
  double  bestError = 1000000000.0;
  double  pRange = double(n);
  while (maxIterations > 0 && bestError > 0.0)
  {
    for (int i = 0; i <= k; i++)
    {
      if ((flags & 0x0040) && i == 0)
        p[i] = 0.0;
      else if ((flags & 0x0080) && i == 1)
        p[i] = double(n >> 1);
      else if ((flags & 0x0100) && i == 2)
        p[i] = double(n);
      else
        p[i] = bestP[i] + getRandom(-pRange, pRange);
      p[i] = (p[i] > 0.0 ? (p[i] < double(n) ? p[i] : double(n)) : 0.0);
    }
    double  minDist = double(n);
    for (int i = 0; i <= k; i++)
    {
      for (int j = i + 1; j <= k; j++)
      {
        double  d = std::fabs(p[j] - p[i]);
        minDist = (d < minDist ? d : minDist);
      }
    }
    if (minDist < 1.0)
      continue;
    maxIterations--;
    if (pRange > (double(n) * 0.0001))
      pRange = pRange * 0.9999;
    for (int i = 0; i <= k; i++)
      y[i] = interpolateFunction(f, n, k, &(m.front()), p[i]);
    for (int i = 0; i <= k; i++)
    {
      m[i * (k + 2)] = 1.0;
      double  x = (p[i] / double(n)) * (x1 - x0) + x0;
      for (int j = 1; j <= k; j++)
        m[i * (k + 2) + j] = m[i * (k + 2) + (j - 1)] * x;
      m[i * (k + 2) + (k + 1)] = y[i];
    }
    solveEquationSystem(&(m.front()), k + 2, k + 1);
    for (int i = 0; i <= k; i++)
      aTmp[i] = m[i * (k + 2) + (k + 1)];
    double  err =
        calculateError(f, n, &(aTmp.front()), k, x0, x1, bestError, flags);
    if ((err + 0.000000000001) < bestError)
    {
      bestError = err;
      for (int i = k; i >= 0; i--)
      {
        a[i] = aTmp[i];
        bestP[i] = p[i];
      }
      if (maxIterations > 0 && maxIterations < 0x10000000)
        continue;
    }
    else if (maxIterations > 0)
    {
      continue;
    }
    for (int i = k; i >= 0; i--)
      std::printf(" %11.8f", a[i]);
    std::printf(", error =%11.8f\n", std::sqrt(bestError / double(n + 1)));
  }
}

YMM_Double srgbCompress(YMM_Double c)
{
  YMM_Double  tmp = c;
  for (int i = 0; i < 3; i++)
  {
    tmp[i] = std::min(std::max(c[i], 0.0), 1.0);
    if (tmp[i] > 0.0031308)
      tmp[i] = std::pow(tmp[i], 1.0 / 2.4) * 1.055 - 0.055;
    else
      tmp[i] *= 12.92;
  }
  return tmp;
}

// T_red = 851.345
// T_white = 6548.04

int main(int argc, char **argv)
{
  YMM_Double  c0 = cieColorTemp(6548.04);
  if (argc > 1)
  {
    for (int i = 1; i < argc; i++)
    {
      double  t = std::atof(argv[i]);
      t = std::pow(6548.04 / 851.345, t) * 6548.04;
      YMM_Double  c = cieColorTemp(t) / c0;
      double  d1 = std::max(c[0], std::max(c[1], c[2]));
      double  d0 = std::min(std::min(c[0], std::min(c[1], c[2])), 0.0);
      c = (c - d0) / (d1 - d0);
      std::printf("%f K: R = %f, G = %f, B = %f\n", t, c[0], c[1], c[2]);
    }
  }
  else
  {
    std::vector< unsigned char >  ddsHeader(148);
    FileBuffer::writeDDSHeader(ddsHeader.data(), 0x18, 1600, 400);
    std::vector< FloatVector4 > outBuf1(1600);
    std::vector< FloatVector4 > outBuf2(1600);
    {
      for (int x = 0; x < 1600; x++)
      {
        double  t = (double(x) / double(1599)) * 2.0 - 1.0;;
        t = std::pow(6548.04 / 851.345, t) * 6548.04;
        YMM_Double  c = cieColorTemp(t) / c0;
        double  d1 = std::max(c[0], std::max(c[1], c[2]));
        double  d0 = std::min(std::min(c[0], std::min(c[1], c[2])), 0.0);
        c = (c - d0) / (d1 - d0);
        c = srgbCompress(c);
        FloatVector4  tmp((float) c[0], (float) c[1], (float) c[2], 1.0f);
        outBuf1[x] = (tmp * 255.0f).shuffleValues(0xC6);
      }
      OutputFile  f("colortemp.dds", 16384);
      f.writeData(ddsHeader.data(), 148);
      for (int y = 0; y < 400; y++)
      {
        for (int x = 0; x < 1600; x++)
        {
          std::uint32_t tmp = outBuf1[x].convertToA2R10G10B10();
          f.writeData(&tmp, sizeof(std::uint32_t));
        }
      }
    }
    int     k = 6;
    std::vector< double > a((k + 1) * 4, 0.0);
    for (int i = 0; i < 4; i++)
    {
      int     n = 512;
      std::vector< double > f(n + 1, 0.0);
      for (int j = 0; j <= n; j++)
      {
        double  t = double(j) / double(n);
        if (i >= 2)
          t = t - 1.0;
        t = std::pow(6548.04 / 851.345, t) * 6548.04;
        YMM_Double  c = cieColorTemp(t) / c0;
        double  d1 = std::max(c[0], std::max(c[1], c[2]));
        double  d0 = std::min(std::min(c[0], std::min(c[1], c[2])), 0.0);
        c = (c - d0) / (d1 - d0);
        f[j] = c[i < 3 ? i : 1];
#if 0
        std::printf("%.10f\n", f[j]);
#endif
      }
      std::printf("Channel %d: f[0] = %.10f, polynomial:\n", i, f[0]);
      unsigned int  flags = 0x0142;
      findPolynomial(a.data() + (i * (k + 1)), k, f.data(), n,
                     (i < 2 ? 0.0 : -1.0), (i < 2 ? 1.0 : 0.0), flags, 1000000);
    }
    {
      for (int x = 0; x < 1600; x++)
      {
        double  t = (double(x) / double(1599)) * 2.0 - 1.0;
        YMM_Double  c = { 0.0, 0.0, 0.0, 0.0 };
        for (int i = k; i >= 0; i--)
        {
          c[0] = c[0] * t + a[i];
          c[1] = c[1] * t + a[i + (k + 1)];
          c[2] = c[2] * t + a[i + (k * 2 + 2)];
          c[3] = c[3] * t + a[i + (k * 3 + 3)];
        }
        c[1] = std::min(c[1], c[3]);
        if (t >= 0.0)
          c[2] = 1.0;
        c[3] = 1.0;
        c = srgbCompress(c);
        FloatVector4  tmp((float) c[0], (float) c[1], (float) c[2], 1.0f);
        outBuf2[x] = (tmp * 255.0f).shuffleValues(0xC6);
      }
      OutputFile  f("colortemp2.dds", 16384);
      f.writeData(ddsHeader.data(), 148);
      for (int y = 0; y < 400; y++)
      {
        for (int x = 0; x < 1600; x++)
        {
          std::uint32_t tmp = outBuf2[x].convertToA2R10G10B10();
          f.writeData(&tmp, sizeof(std::uint32_t));
        }
      }
    }
    double  avgErr = 0.0;
    double  maxErr = 0.0;
    for (int i = 0; i < 1600; i++)
    {
      FloatVector4  err(outBuf2[i] - outBuf1[i]);
      avgErr = avgErr + err.dotProduct3(err);
      maxErr = std::max(maxErr, double(std::fabs(err[0])));
      maxErr = std::max(maxErr, double(std::fabs(err[1])));
      maxErr = std::max(maxErr, double(std::fabs(err[2])));
    }
    avgErr = std::sqrt(avgErr / double(1600 * 3));
    std::printf("Avg. error = %f, max. error = %f\n", avgErr, maxErr);
  }
  return 0;
}

