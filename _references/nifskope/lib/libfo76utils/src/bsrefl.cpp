
#include "common.hpp"
#include "bsrefl.hpp"

void BSReflStream::readStringTable()
{
  if (fileBufSize < 24 ||
      readUInt64() != 0x0000000848544542ULL)            // "BETH", 8
  {
    errorMessage("invalid reflection stream header");
  }
  if (readUInt32Fast() != 4U)
    errorMessage("unsupported reflection stream version");
  chunksRemaining = readUInt32Fast();
  if (chunksRemaining < 2U || readUInt32Fast() != ChunkType_STRT)
    errorMessage("missing string table in reflection stream");
  chunksRemaining = chunksRemaining - 2U;
  // create string table
  unsigned int  n = readUInt32Fast();
  if ((filePos + std::uint64_t(n)) > fileBufSize)
    errorMessage("unexpected end of reflection stream");
  stringMap.resize(n, std::int16_t(-1));
  for ( ; n; n--)
  {
    unsigned int  strtOffs = (unsigned int) (filePos - 24);
    const char  *s = reinterpret_cast< const char * >(fileBuf + filePos);
    for ( ; readUInt8Fast(); n--)
    {
      if (!n)
        errorMessage("string table is not terminated in reflection stream");
      stringMap[strtOffs] = std::int16_t(findString(s));
      if (stringMap[strtOffs] < 0)
      {
        std::fprintf(stderr,
                     "Warning: unrecognized string in reflection stream: "
                     "'%s'\n", s);
      }
    }
  }
}

int BSReflStream::findString(const char *s)
{
  size_t  n0 = 19;
  size_t  n2 = sizeof(stringTable) / sizeof(char *);
  while (n2 > (n0 + 1))
  {
    size_t  n1 = (n0 + n2) >> 1;
    if (std::strcmp(s, stringTable[n1]) < 0)
      n2 = n1;
    else
      n0 = n1;
  }
  if (n2 > n0 && std::strcmp(s, stringTable[n0]) == 0)
    return int(n0);
  return -1;
}

std::uint32_t BSReflStream::findString(unsigned int strtOffs) const
{
  if (strtOffs < stringMap.size())
  {
    int     n = stringMap[strtOffs];
    if (n >= 0)
      return size_t(n);
  }
  unsigned int  n = strtOffs - 0xFFFFFF01U;
  return std::uint32_t(std::min(n, 18U));
}

bool BSReflStream::Chunk::readEnum(unsigned char& n, const char *t)
{
  if ((filePos + 2ULL) > fileBufSize) [[unlikely]]
  {
    filePos = fileBufSize;
    return false;
  }
  unsigned int  len = readUInt16Fast();
  if ((filePos + std::uint64_t(len)) > fileBufSize) [[unlikely]]
  {
    filePos = fileBufSize;
    return false;
  }
  const char  *s = reinterpret_cast< const char * >(fileBuf + filePos);
  filePos = filePos + len;
  while (len > 0U && s[len - 1U] == '\0')
    len--;
  for (unsigned int i = 0U; *t; i++)
  {
    unsigned int  len2 = (unsigned char) *t;
    const char  *s2 = t + 1;
    t = s2 + len2;
    if (len2 != len)
      continue;
    for (unsigned int j = 0U; len2 && s2[j] == s[j]; j++, len2--)
      ;
    if (!len2)
    {
      n = (unsigned char) i;
      break;
    }
  }
  return true;
}

void BSReflDump::dumpItem(std::string& s, Chunk& chunkBuf, bool isDiff,
                          std::uint32_t itemType, int indentCnt)
{
  const CDBClassDef *classDef = nullptr;
  if (itemType > String_Unknown)
  {
    std::map< std::uint32_t, CDBClassDef >::const_iterator
        i = classes.find(itemType);
    if (i != classes.end()) [[likely]]
      classDef = &(i->second);
    else
      itemType = String_Unknown;
  }
  if (itemType > String_Unknown)
  {
    printToString(s, "{\n%*s\"Data\": {", indentCnt + 1, "");
    unsigned int  nMax = (unsigned int) classDef->fields.size();
    bool    firstField = true;
    if (classDef->isUser)
    {
      Chunk userBuf;
      unsigned int  userChunkType = readChunk(userBuf);
      if (userChunkType != ChunkType_USER && userChunkType != ChunkType_USRD)
      {
        throw NifSkopeError("unexpected chunk type in reflection stream "
                            "at 0x%08x", (unsigned int) getPosition());
      }
      isDiff = (userChunkType == ChunkType_USRD);
      std::uint32_t className1 = findString(userBuf.readUInt32());
      if (className1 != itemType)
      {
        throw NifSkopeError("USER chunk has unexpected type at 0x%08x",
                            (unsigned int) getPosition());
      }
      std::uint32_t className2 = findString(userBuf.readUInt32());
      if (className2 == className1)
      {
        // no type conversion
        nMax--;
        for (unsigned int n = 0U - 1U;
             userBuf.getFieldNumber(n, nMax, isDiff); )
        {
          printToString(s, (!firstField ? ",\n%*s\"%s\": " : "\n%*s\"%s\": "),
                        indentCnt + 2, "",
                        stringTable[classDef->fields[n].first]);
          dumpItem(s, userBuf, isDiff, classDef->fields[n].second,
                   indentCnt + 2);
          firstField = false;
        }
      }
      else if (className2 < String_Unknown)
      {
        unsigned int  n = 0U;
        do
        {
          const char  *fieldNameStr = "null";
          if (nMax) [[likely]]
            fieldNameStr = stringTable[classDef->fields[n].first];
          printToString(s, (!firstField ? ",\n%*s\"%s\": " : "\n%*s\"%s\": "),
                        indentCnt + 2, "", fieldNameStr);
          dumpItem(s, userBuf, isDiff, className2, indentCnt + 2);
          firstField = false;
          className2 = findString(userBuf.readUInt32());
        }
        while (++n < nMax && className2 < String_Unknown);
      }
    }
    else
    {
      nMax--;
      for (unsigned int n = 0U - 1U; chunkBuf.getFieldNumber(n, nMax, isDiff); )
      {
        printToString(s, (!firstField ? ",\n%*s\"%s\": " : "\n%*s\"%s\": "),
                      indentCnt + 2, "",
                      stringTable[classDef->fields[n].first]);
        dumpItem(s, chunkBuf, isDiff, classDef->fields[n].second,
                 indentCnt + 2);
        firstField = false;
      }
    }
    if (!s.ends_with('{'))
      printToString(s, "\n%*s", indentCnt + 1, "");
    printToString(s, "},\n%*s\"Type\": \"%s\"\n",
                  indentCnt + 1, "", stringTable[itemType]);
    printToString(s, "%*s}", indentCnt, "");
    return;
  }
  FileBuffer& buf2 = *(static_cast< FileBuffer * >(&chunkBuf));
  switch (itemType)
  {
    case String_None:
      printToString(s, "null");
      break;
    case String_String:
      {
        unsigned int  len = buf2.readUInt16();
        bool    endOfString = false;
        s += '"';
        while (len--)
        {
          char    c = char(buf2.readUInt8());
          if (!endOfString)
          {
            if (!c)
            {
              endOfString = true;
              continue;
            }
            if ((unsigned char) c < 0x20 || c == '"' || c == '\\')
            {
              s += '\\';
              switch (c)
              {
                case '\b':
                  c = 'b';
                  break;
                case '\t':
                  c = 't';
                  break;
                case '\n':
                  c = 'n';
                  break;
                case '\f':
                  c = 'f';
                  break;
                case '\r':
                  c = 'r';
                  break;
                default:
                  if ((unsigned char) c < 0x20)
                  {
                    printToString(s, "u%04X", (unsigned int) c);
                    continue;
                  }
                  break;
              }
            }
            s += c;
          }
        }
        s += '"';
      }
      break;
    case String_List:
      {
        Chunk listBuf;
        unsigned int  chunkType = readChunk(listBuf);
        if (chunkType != ChunkType_LIST)
        {
          throw NifSkopeError("unexpected chunk type in reflection stream "
                              "at 0x%08x", (unsigned int) getPosition());
        }
        std::uint32_t elementType = findString(listBuf.readUInt32());
        std::uint32_t listSize = 0U;
        if ((listBuf.getPosition() + 4ULL) <= listBuf.size())
          listSize = listBuf.readUInt32();
        printToString(s, "{\n%*s\"Data\": [", indentCnt + 1, "");
        for (std::uint32_t i = 0U; i < listSize; i++)
        {
          printToString(s, "\n%*s", indentCnt + 2, "");
          dumpItem(s, listBuf, isDiff, elementType, indentCnt + 2);
          if ((i + 1U) < listSize)
            printToString(s, ",");
          else
            printToString(s, "\n%*s", indentCnt + 1, "");
        }
        printToString(s, "],\n");
        if (listSize)
        {
          const char  *elementTypeStr = stringTable[elementType];
          if (elementType && elementType < String_Ref)
          {
            elementTypeStr = (elementType == String_String ?
                              "BSFixedString" : "<collection>");
          }
          printToString(s, "%*s\"ElementType\": \"%s\",\n",
                        indentCnt + 1, "", elementTypeStr);
        }
        printToString(s, "%*s\"Type\": \"<collection>\"\n", indentCnt + 1, "");
        printToString(s, "%*s}", indentCnt, "");
      }
      break;
    case String_Map:
      {
        Chunk mapBuf;
        unsigned int  chunkType = readChunk(mapBuf);
        if (chunkType != ChunkType_MAPC)
        {
          throw NifSkopeError("unexpected chunk type in reflection stream "
                              "at 0x%08x", (unsigned int) getPosition());
        }
        std::uint32_t keyClassName = findString(mapBuf.readUInt32());
        std::uint32_t valueClassName = findString(mapBuf.readUInt32());
        std::uint32_t mapSize = 0U;
        if ((mapBuf.getPosition() + 4ULL) <= mapBuf.size())
          mapSize = mapBuf.readUInt32();
        printToString(s, "{\n%*s\"Data\": [", indentCnt + 1, "");
        for (std::uint32_t i = 0U; i < mapSize; i++)
        {
          printToString(s, "\n%*s{", indentCnt + 2, "");
          printToString(s, "\n%*s\"Data\": {", indentCnt + 3, "");
          printToString(s, "\n%*s\"Key\": ", indentCnt + 4, "");
          dumpItem(s, mapBuf, false, keyClassName, indentCnt + 4);
          printToString(s, ",\n%*s\"Value\": ", indentCnt + 4, "");
          dumpItem(s, mapBuf, isDiff, valueClassName, indentCnt + 4);
          printToString(s, "\n%*s},", indentCnt + 3, "");
          printToString(s, "\n%*s\"Type\": \"StdMapType::Pair\"\n",
                        indentCnt + 3, "");
          printToString(s, "%*s}", indentCnt + 2, "");
          if ((i + 1U) < mapSize)
            printToString(s, ",");
          else
            printToString(s, "\n%*s", indentCnt + 1, "");
        }
        printToString(s, "],\n%*s\"ElementType\": \"StdMapType::Pair\",\n",
                      indentCnt + 1, "");
        printToString(s, "%*s\"Type\": \"<collection>\"\n", indentCnt + 1, "");
        printToString(s, "%*s}", indentCnt, "");
      }
      break;
    case String_Ref:
      {
        std::uint32_t refType = findString(buf2.readUInt32());
        printToString(s, "{\n%*s\"Data\": ", indentCnt + 1, "");
        dumpItem(s, chunkBuf, isDiff, refType, indentCnt + 1);
        printToString(s, ",\n%*s\"Type\": \"<ref>\"\n", indentCnt + 1, "");
        printToString(s, "%*s}", indentCnt, "");
      }
      break;
    case String_Int8:
      printToString(s, "%d", int(std::int8_t(buf2.readUInt8())));
      break;
    case String_UInt8:
      printToString(s, "%u", (unsigned int) buf2.readUInt8());
      break;
    case String_Int16:
      printToString(s, "%d", int(std::int16_t(buf2.readUInt16())));
      break;
    case String_UInt16:
      printToString(s, "%u", (unsigned int) buf2.readUInt16());
      break;
    case String_Int32:
      printToString(s, "%d", int(buf2.readInt32()));
      break;
    case String_UInt32:
      printToString(s, "%u", (unsigned int) buf2.readUInt32());
      break;
    case String_Int64:
      printToString(s, "\"%lld\"", (long long) std::int64_t(buf2.readUInt64()));
      break;
    case String_UInt64:
      printToString(s, "\"%llu\"", (unsigned long long) buf2.readUInt64());
      break;
    case String_Bool:
      printToString(s, "%s", (!buf2.readUInt8() ? "false" : "true"));
      break;
    case String_Float:
      printToString(s, "%.7g", buf2.readFloat());
      break;
    case String_Double:
      // FIXME: implement this in a portable way
      printToString(s, "%.14g",
                    std::bit_cast< double, std::uint64_t >(buf2.readUInt64()));
      break;
    default:
      printToString(s, "<unknown>");
      chunkBuf.setPosition(chunkBuf.size());
      break;
  }
}

void BSReflDump::readAllChunks(std::string& s, int indentCnt, bool verboseMode)
{
  Chunk   chunkBuf;
  unsigned int  chunkType;
  bool    isArray = false;
  bool    firstObject = true;
  size_t  objectCnt = 0;
  for (size_t i = filePos; (i + 8ULL) <= fileBufSize; )
  {
    chunkType = FileBuffer::readUInt32Fast(fileBuf + i);
    size_t  chunkSize = FileBuffer::readUInt32Fast(fileBuf + (i + 4));
    if (chunkType == ChunkType_DIFF || chunkType == ChunkType_OBJT ||
        (verboseMode && chunkType == ChunkType_CLAS))
    {
      if (++objectCnt > 1)
      {
        isArray = true;
        break;
      }
    }
    if ((i + (std::uint64_t(chunkSize) + 8ULL)) >= fileBufSize)
      break;
    i = i + (chunkSize + 8);
  }
  if (isArray)
  {
    indentCnt++;
    printToString(s, "{ \"Objects\": [\n%*s", indentCnt, "");
  }
  while ((chunkType = readChunk(chunkBuf)) != 0U)
  {
    if (chunkType == ChunkType_CLAS)
    {
      std::uint32_t className = findString(chunkBuf.readUInt32());
      if (className < String_Unknown)
        errorMessage("invalid class ID in reflection stream");
      unsigned int  classVersion = chunkBuf.readUInt32();
      unsigned int  classFlags = chunkBuf.readUInt16();
      (void) chunkBuf.readUInt16();     // number of fields
      unsigned int  fieldCnt = 0U;
      if (verboseMode)
      {
        if (!firstObject)
          printToString(s, ",\n%*s", indentCnt, "");
        firstObject = false;
        printToString(s, "{\n%*s\"Fields\": [", indentCnt + 1, "");
      }
      CDBClassDef&  classDef = classes[className];
      classDef.isUser = bool(classFlags & 4U);
      for ( ; (chunkBuf.getPosition() + 12ULL) <= chunkBuf.size(); fieldCnt++)
      {
        std::uint32_t fieldName = findString(chunkBuf.readUInt32Fast());
        if (fieldName < String_Unknown)
        {
          errorMessage("invalid field name in class definition "
                       "in reflection stream");
        }
        std::uint32_t fieldType = findString(chunkBuf.readUInt32Fast());
        unsigned int  dataOffset = chunkBuf.readUInt16Fast();
        unsigned int  dataSize = chunkBuf.readUInt16Fast();
        if (verboseMode)
        {
          printToString(s, (!fieldCnt ? "\n%*s" : ",\n%*s"), indentCnt + 2, "");
          printToString(s, "{\n%*s\"Name\": \"%s\",",
                        indentCnt + 3, "", stringTable[fieldName]);
          printToString(s, "\n%*s\"Offset\": %u,",
                        indentCnt + 3, "", dataOffset);
          printToString(s, "\n%*s\"Size\": %u,", indentCnt + 3, "", dataSize);
          printToString(s, "\n%*s\"Type\": \"%s\"",
                        indentCnt + 3, "", stringTable[fieldType]);
          printToString(s, "\n%*s}", indentCnt + 2, "");
        }
        classDef.fields.emplace_back(fieldName, fieldType);
      }
      if (verboseMode)
      {
        if (fieldCnt)
          printToString(s, "\n%*s", indentCnt + 1, "");
        printToString(s, "],\n%*s\"Flags\": %u,",
                      indentCnt + 1, "", classFlags);
        printToString(s, "\n%*s\"Name\": \"%s\",",
                      indentCnt + 1, "", stringTable[className]);
        printToString(s, "\n%*s\"Type\": \"<class>\",", indentCnt + 1, "");
        printToString(s, "\n%*s\"Version\": %u",
                      indentCnt + 1, "", classVersion);
        printToString(s, "\n%*s}", indentCnt, "");
      }
      continue;
    }
    if (chunkType == ChunkType_DIFF || chunkType == ChunkType_OBJT) [[likely]]
    {
      if (!firstObject)
        printToString(s, ",\n%*s", indentCnt, "");
      std::uint32_t className = findString(chunkBuf.readUInt32());
      bool    isDiff = (chunkType == ChunkType_DIFF);
      dumpItem(s, chunkBuf, isDiff, className, indentCnt);
      firstObject = false;
      continue;
    }
    if (chunkType != ChunkType_TYPE)
    {
      throw NifSkopeError("unexpected reflection stream chunk type 0x%08X",
                          chunkType);
    }
  }
  if (!firstObject)
    printToString(s, "\n");
  if (isArray)
  {
    indentCnt--;
    if (s.ends_with('\n'))
      printToString(s, "%*s] }\n", indentCnt, "");
    else
      printToString(s, "] }\n");
  }
  bool    isIndent = true;
  for (size_t i = 0; i < s.length(); i++)
  {
    if (s[i] == ' ' && isIndent)
      s[i] = '\t';
    else
      isIndent = (s[i] == '\n');
  }
}

const char * BSReflStream::stringTable[1157] =
{
  "null",                                               //    0
  "String",                                             //    1
  "List",                                               //    2
  "Map",                                                //    3
  "<ref>",                                              //    4
  "Unknown_-250",                                       //    5
  "Unknown_-249",                                       //    6
  "int8_t",                                             //    7
  "uint8_t",                                            //    8
  "int16_t",                                            //    9
  "uint16_t",                                           //   10
  "int32_t",                                            //   11
  "uint32_t",                                           //   12
  "int64_t",                                            //   13
  "uint64_t",                                           //   14
  "bool",                                               //   15
  "float",                                              //   16
  "double",                                             //   17
  "<unknown>",                                          //   18
  "AOVertexColorChannel",                               //   19
  "Absolute",                                           //   20
  "ActiveLayersMask",                                   //   21
  "ActorValueBindings",                                 //   22
  "ActorValueSnapshot",                                 //   23
  "ActorValueSnapshot::ActorValueBinding",              //   24
  "AdaptSpeedDown",                                     //   25
  "AdaptSpeedUp",                                       //   26
  "AdaptiveEmittance",                                  //   27
  "Address",                                            //   28
  "AerosolAbsorbtion",                                  //   29
  "AerosolDensity",                                     //   30
  "AerosolPhaseFunction",                               //   31
  "Albedo",                                             //   32
  "AlbedoPaletteTex",                                   //   33
  "AlbedoTint",                                         //   34
  "AlbedoTintColor",                                    //   35
  "AllowReapply",                                       //   36
  "Alpha",                                              //   37
  "AlphaAdd",                                           //   38
  "AlphaBias",                                          //   39
  "AlphaChannel",                                       //   40
  "AlphaDistance",                                      //   41
  "AlphaMultiply",                                      //   42
  "AlphaPaletteTex",                                    //   43
  "AlphaTestThreshold",                                 //   44
  "AlphaTint",                                          //   45
  "AmbientOcclusion",                                   //   46
  "Anchor",                                             //   47
  "AnimatedDecalIgnoresTAA",                            //   48
  "Anisotropy",                                         //   49
  "Aperture",                                           //   50
  "ApplyExternalEmittanceToAllMaterials",               //   51
  "ApplyFlowOnANMR",                                    //   52
  "ApplyFlowOnEmissivity",                              //   53
  "ApplyFlowOnOpacity",                                 //   54
  "ApplyNodeRotation",                                  //   55
  "ApplyToAllChildren",                                 //   56
  "AssetDataA",                                         //   57
  "AssetName",                                          //   58
  "AtmosphereType",                                     //   59
  "AttachSoundToRefr",                                  //   60
  "AttachType",                                         //   61
  "Attachment",                                         //   62
  "AutoExposure",                                       //   63
  "AutoPlay",                                           //   64
  "BGSAtmosphere",                                      //   65
  "BGSAtmosphere::AtmosphereSettings",                  //   66
  "BGSAtmosphere::CelestialBodySettings",               //   67
  "BGSAtmosphere::MiscSettings",                        //   68
  "BGSAtmosphere::OverrideSettings",                    //   69
  "BGSAtmosphere::ScatteringSettings",                  //   70
  "BGSAtmosphere::ScatteringSettings::MieSettings",     //   71
  "BGSAtmosphere::ScatteringSettings::RayleighSettings", //   72
  "BGSAtmosphere::StarSettings",                        //   73
  "BGSAudio::WwiseGUID",                                //   74
  "BGSAudio::WwiseSoundHook",                           //   75
  "BGSCloudForm",                                       //   76
  "BGSCloudForm::CloudLayer",                           //   77
  "BGSCloudForm::CloudPlane",                           //   78
  "BGSCloudForm::ShadowParams",                         //   79
  "BGSCurve3DForm",                                     //   80
  "BGSCurveForm",                                       //   81
  "BGSEffectSequenceForm",                              //   82
  "BGSEffectSequenceFormComponent",                     //   83
  "BGSFogVolumeForm",                                   //   84
  "BGSForceData",                                       //   85
  "BGSFormFolderKeywordList",                           //   86
  "BGSLayeredMaterialSwap",                             //   87
  "BGSLayeredMaterialSwap::Entry",                      //   88
  "BGSLodOwnerComponent",                               //   89
  "BGSMaterialPathForm",                                //   90
  "BGSMaterialPropertyComponent",                       //   91
  "BGSMaterialPropertyComponent::Entry",                //   92
  "BGSParticleSystemDefineCollection",                  //   93
  "BGSSpacePhysicsFormComponent",                       //   94
  "BGSTimeOfDayData",                                   //   95
  "BGSVolumetricLighting",                              //   96
  "BGSVolumetricLightingSettings",                      //   97
  "BGSVolumetricLightingSettings::DistantLightingSettings", //   98
  "BGSVolumetricLightingSettings::ExteriorAndInteriorSettings", //   99
  "BGSVolumetricLightingSettings::ExteriorSettings",    //  100
  "BGSVolumetricLightingSettings::FogDensitySettings",  //  101
  "BGSVolumetricLightingSettings::FogMapSettings",      //  102
  "BGSVolumetricLightingSettings::FogThicknessSettings", //  103
  "BGSVolumetricLightingSettings::HorizonFogSettings",  //  104
  "BGSWeatherSettingsForm",                             //  105
  "BGSWeatherSettingsForm::ColorSettings",              //  106
  "BGSWeatherSettingsForm::FoliageSettings",            //  107
  "BGSWeatherSettingsForm::MagicEffect",                //  108
  "BGSWeatherSettingsForm::PrecipitationSettings",      //  109
  "BGSWeatherSettingsForm::SoundEffectSettings",        //  110
  "BGSWeatherSettingsForm::SpellEffect",                //  111
  "BGSWeatherSettingsForm::SpellSettings",              //  112
  "BGSWeatherSettingsForm::WeatherChoiceSettings",      //  113
  "BGSWeatherSettingsForm::WeatherSound",               //  114
  "BSAttachConfig::ArtObjectAttach",                    //  115
  "BSAttachConfig::AttachmentConfiguration",            //  116
  "BSAttachConfig::LensFlareAttachment",                //  117
  "BSAttachConfig::LightAttachment",                    //  118
  "BSAttachConfig::NodeAttachment",                     //  119
  "BSAttachConfig::NodeName",                           //  120
  "BSAttachConfig::ObjectAttachment",                   //  121
  "BSAttachConfig::ParticleAttachment",                 //  122
  "BSAttachConfig::SearchRootNode",                     //  123
  "BSAttachConfig::SearchSingleNameSingleNode",         //  124
  "BSAttachConfig::SoundAttachment",                    //  125
  "BSBind::Address",                                    //  126
  "BSBind::ColorCurveController",                       //  127
  "BSBind::ColorLerpController",                        //  128
  "BSBind::ComponentProperty",                          //  129
  "BSBind::ControllerComponent",                        //  130
  "BSBind::Controllers",                                //  131
  "BSBind::Controllers::Mapping",                       //  132
  "BSBind::Directory",                                  //  133
  "BSBind::DirectoryComponent",                         //  134
  "BSBind::Float2DCurveController",                     //  135
  "BSBind::Float2DLerpController",                      //  136
  "BSBind::Float3DCurveController",                     //  137
  "BSBind::Float3DLerpController",                      //  138
  "BSBind::FloatCurveController",                       //  139
  "BSBind::FloatLerpController",                        //  140
  "BSBind::Multiplex",                                  //  141
  "BSBind::Snapshot",                                   //  142
  "BSBind::Snapshot::Entry",                            //  143
  "BSBind::TimerController",                            //  144
  "BSBlendable::ColorValue",                            //  145
  "BSBlendable::FloatValue",                            //  146
  "BSColorCurve",                                       //  147
  "BSComponentDB2::DBFileIndex",                        //  148
  "BSComponentDB2::DBFileIndex::ComponentInfo",         //  149
  "BSComponentDB2::DBFileIndex::ComponentTypeInfo",     //  150
  "BSComponentDB2::DBFileIndex::EdgeInfo",              //  151
  "BSComponentDB2::DBFileIndex::ObjectInfo",            //  152
  "BSComponentDB2::ID",                                 //  153
  "BSComponentDB::CTName",                              //  154
  "BSFloat2DCurve",                                     //  155
  "BSFloat3DCurve",                                     //  156
  "BSFloatCurve",                                       //  157
  "BSFloatCurve::Control",                              //  158
  "BSGalaxy::BGSSunPresetForm",                         //  159
  "BSGalaxy::BGSSunPresetForm::DawnDuskSettings",       //  160
  "BSGalaxy::BGSSunPresetForm::NightSettings",          //  161
  "BSHoudini::HoudiniAssetData",                        //  162
  "BSHoudini::HoudiniAssetData::Parameter",             //  163
  "BSMaterial::AlphaBlenderSettings",                   //  164
  "BSMaterial::AlphaSettingsComponent",                 //  165
  "BSMaterial::BlendModeComponent",                     //  166
  "BSMaterial::BlendParamFloat",                        //  167
  "BSMaterial::BlenderID",                              //  168
  "BSMaterial::Channel",                                //  169
  "BSMaterial::CollisionComponent",                     //  170
  "BSMaterial::Color",                                  //  171
  "BSMaterial::ColorChannelTypeComponent",              //  172
  "BSMaterial::ColorRemapSettingsComponent",            //  173
  "BSMaterial::DecalSettingsComponent",                 //  174
  "BSMaterial::DetailBlenderSettings",                  //  175
  "BSMaterial::DetailBlenderSettingsComponent",         //  176
  "BSMaterial::DistortionComponent",                    //  177
  "BSMaterial::EffectSettingsComponent",                //  178
  "BSMaterial::EmissiveSettingsComponent",              //  179
  "BSMaterial::EmittanceSettings",                      //  180
  "BSMaterial::EyeSettingsComponent",                   //  181
  "BSMaterial::FlipbookComponent",                      //  182
  "BSMaterial::FlowSettingsComponent",                  //  183
  "BSMaterial::GlobalLayerDataComponent",               //  184
  "BSMaterial::GlobalLayerNoiseSettings",               //  185
  "BSMaterial::HairSettingsComponent",                  //  186
  "BSMaterial::Internal::CompiledDB",                   //  187
  "BSMaterial::Internal::CompiledDB::FilePair",         //  188
  "BSMaterial::LODMaterialID",                          //  189
  "BSMaterial::LayerID",                                //  190
  "BSMaterial::LayeredEdgeFalloffComponent",            //  191
  "BSMaterial::LayeredEmissivityComponent",             //  192
  "BSMaterial::LevelOfDetailSettings",                  //  193
  "BSMaterial::MRTextureFile",                          //  194
  "BSMaterial::MaterialID",                             //  195
  "BSMaterial::MaterialOverrideColorTypeComponent",     //  196
  "BSMaterial::MaterialParamFloat",                     //  197
  "BSMaterial::MipBiasSetting",                         //  198
  "BSMaterial::MouthSettingsComponent",                 //  199
  "BSMaterial::Offset",                                 //  200
  "BSMaterial::OpacityComponent",                       //  201
  "BSMaterial::ParamBool",                              //  202
  "BSMaterial::PhysicsMaterialType",                    //  203
  "BSMaterial::ProjectedDecalSettings",                 //  204
  "BSMaterial::Scale",                                  //  205
  "BSMaterial::ShaderModelComponent",                   //  206
  "BSMaterial::ShaderRouteComponent",                   //  207
  "BSMaterial::SourceTextureWithReplacement",           //  208
  "BSMaterial::StarmapBodyEffectComponent",             //  209
  "BSMaterial::TerrainSettingsComponent",               //  210
  "BSMaterial::TerrainTintSettingsComponent",           //  211
  "BSMaterial::TextureAddressModeComponent",            //  212
  "BSMaterial::TextureFile",                            //  213
  "BSMaterial::TextureReplacement",                     //  214
  "BSMaterial::TextureResolutionSetting",               //  215
  "BSMaterial::TextureSetID",                           //  216
  "BSMaterial::TextureSetKindComponent",                //  217
  "BSMaterial::TranslucencySettings",                   //  218
  "BSMaterial::TranslucencySettingsComponent",          //  219
  "BSMaterial::UVStreamID",                             //  220
  "BSMaterial::UVStreamParamBool",                      //  221
  "BSMaterial::VegetationSettingsComponent",            //  222
  "BSMaterial::WaterFoamSettingsComponent",             //  223
  "BSMaterial::WaterGrimeSettingsComponent",            //  224
  "BSMaterial::WaterSettingsComponent",                 //  225
  "BSMaterialBinding::MaterialPropertyNode",            //  226
  "BSMaterialBinding::MaterialUVStreamPropertyNode",    //  227
  "BSResource::ID",                                     //  228
  "BSSequence::AnimationEvent",                         //  229
  "BSSequence::AnimationTrack",                         //  230
  "BSSequence::CameraShakeEvent",                       //  231
  "BSSequence::CameraShakeStrengthTrack",               //  232
  "BSSequence::CameraShakeTrack",                       //  233
  "BSSequence::ColorCurveEvent",                        //  234
  "BSSequence::ColorLerpEvent",                         //  235
  "BSSequence::ColorTriggerEvent",                      //  236
  "BSSequence::CullEvent",                              //  237
  "BSSequence::DissolveEvent",                          //  238
  "BSSequence::DissolveFrequencyScaleTrack",            //  239
  "BSSequence::DissolveOffsetTrack",                    //  240
  "BSSequence::DissolveTrack",                          //  241
  "BSSequence::ExplosionObjectSpawn",                   //  242
  "BSSequence::Float2LerpEvent",                        //  243
  "BSSequence::Float2TriggerEvent",                     //  244
  "BSSequence::FloatCurveEvent",                        //  245
  "BSSequence::FloatLerpEvent",                         //  246
  "BSSequence::FloatNoiseEvent",                        //  247
  "BSSequence::FloatTriggerEvent",                      //  248
  "BSSequence::ImageSpaceLifetimeEvent",                //  249
  "BSSequence::ImageSpaceStrengthTrack",                //  250
  "BSSequence::ImageSpaceTrack",                        //  251
  "BSSequence::ImpactEffectEvent",                      //  252
  "BSSequence::ImpactEffectTrack",                      //  253
  "BSSequence::LightColorTrack",                        //  254
  "BSSequence::LightEffectDirectReferenceTrack",        //  255
  "BSSequence::LightEffectReferenceTrack",              //  256
  "BSSequence::LightEffectTrack",                       //  257
  "BSSequence::LightIntensityTrack",                    //  258
  "BSSequence::LightLensFlareVisiblityTrack",           //  259
  "BSSequence::LightRadiusTrack",                       //  260
  "BSSequence::LightSpawnEvent",                        //  261
  "BSSequence::LoopMarker",                             //  262
  "BSSequence::MaterialFlipbookIndexGeneratorEvent",    //  263
  "BSSequence::MaterialFlipbookIndexTrack",             //  264
  "BSSequence::MaterialPropertyTrack",                  //  265
  "BSSequence::MaterialTrack",                          //  266
  "BSSequence::NoteEvent",                              //  267
  "BSSequence::NoteTrack",                              //  268
  "BSSequence::ObjectAttachmentTrack",                  //  269
  "BSSequence::ObjectSpawnEvent",                       //  270
  "BSSequence::ObjectSpawnTrack",                       //  271
  "BSSequence::ParticleEffectReferenceTrack",           //  272
  "BSSequence::ParticleEffectTrack",                    //  273
  "BSSequence::ParticleEvent",                          //  274
  "BSSequence::ParticleParameterTrack",                 //  275
  "BSSequence::PlaySubSequenceEvent",                   //  276
  "BSSequence::PositionTrack",                          //  277
  "BSSequence::ProjectedDecalAlphaTrack",               //  278
  "BSSequence::ProjectedDecalSpawnEvent",               //  279
  "BSSequence::ProjectedDecalTrack",                    //  280
  "BSSequence::PropertyControllerEvent",                //  281
  "BSSequence::PropertyLerpControllerEvent",            //  282
  "BSSequence::ReferenceSpawnEvent",                    //  283
  "BSSequence::RevertMaterialOverrideEvent",            //  284
  "BSSequence::RotationTrack",                          //  285
  "BSSequence::ScaleTrack",                             //  286
  "BSSequence::SceneNodeTrack",                         //  287
  "BSSequence::Sequence",                               //  288
  "BSSequence::SetPropertyEvent",                       //  289
  "BSSequence::SoundEvent",                             //  290
  "BSSequence::SoundTrack",                             //  291
  "BSSequence::SubSequenceTrack",                       //  292
  "BSSequence::TrackGroup",                             //  293
  "BSSequence::TriggerMaterialSwap",                    //  294
  "BSSequence::VectorCurveEvent",                       //  295
  "BSSequence::VectorLerpEvent",                        //  296
  "BSSequence::VectorNoiseEvent",                       //  297
  "BSSequence::VectorTriggerEvent",                     //  298
  "BSSequence::VisibilityTrack",                        //  299
  "BSTRange<float>",                                    //  300
  "BSTRange<uint32_t>",                                 //  301
  "BackLightingEnable",                                 //  302
  "BackLightingTintColor",                              //  303
  "BacklightingScale",                                  //  304
  "BacklightingSharpness",                              //  305
  "BacklightingTransparencyFactor",                     //  306
  "BackscatterStrength",                                //  307
  "BackscatterWrap",                                    //  308
  "BaseDistance",                                       //  309
  "BaseFrequency",                                      //  310
  "BeginVisualEffect",                                  //  311
  "Bias",                                               //  312
  "Binding",                                            //  313
  "BindingType",                                        //  314
  "BlendAmount",                                        //  315
  "BlendContrast",                                      //  316
  "BlendMaskContrast",                                  //  317
  "BlendMaskPosition",                                  //  318
  "BlendMode",                                          //  319
  "BlendNormalsAdditively",                             //  320
  "BlendPosition",                                      //  321
  "BlendSoftness",                                      //  322
  "Blender",                                            //  323
  "BlendingMode",                                       //  324
  "BlockClothWindIfInterior",                           //  325
  "Bloom",                                              //  326
  "BloomRangeScale",                                    //  327
  "BloomScale",                                         //  328
  "BloomThresholdOffset",                               //  329
  "BlueChannel",                                        //  330
  "Blur",                                               //  331
  "BlurRadiusValue",                                    //  332
  "BlurStrength",                                       //  333
  "BottomBlendDistanceKm",                              //  334
  "BottomBlendStartKm",                                 //  335
  "BranchFlexibility",                                  //  336
  "Brightness",                                         //  337
  "BuildVersion",                                       //  338
  "CameraDistanceFade",                                 //  339
  "CameraExposure",                                     //  340
  "CameraExposureMode",                                 //  341
  "Category",                                           //  342
  "CelestialBodies",                                    //  343
  "CelestialBodyIlluminanceScale",                      //  344
  "CelestialBodyIlluminanceScaleOverride",              //  345
  "CelestialBodyIndirectIlluminanceScaleOverride",      //  346
  "CenterXValue",                                       //  347
  "CenterYValue",                                       //  348
  "Channel",                                            //  349
  "Children",                                           //  350
  "Cinematic",                                          //  351
  "Circular",                                           //  352
  "Class",                                              //  353
  "ClassReference",                                     //  354
  "ClearDissolveOnEnd",                                 //  355
  "CloudDirectLightingContribution",                    //  356
  "CloudIndirectLightingContribution",                  //  357
  "Collapsed",                                          //  358
  "Collisions",                                         //  359
  "Color",                                              //  360
  "ColorAtTime",                                        //  361
  "ColorGradingAmount",                                 //  362
  "ColorGradingTexture",                                //  363
  "ColorTexture",                                       //  364
  "Colors",                                             //  365
  "Columns",                                            //  366
  "ComponentIndex",                                     //  367
  "ComponentType",                                      //  368
  "ComponentTypes",                                     //  369
  "Components",                                         //  370
  "Config",                                             //  371
  "ConfigurableLODData<3, 3>",                          //  372
  "ConfigurableLODData<4, 4>",                          //  373
  "ContactShadowSoftening",                             //  374
  "Contrast",                                           //  375
  "Controller",                                         //  376
  "Controllers",                                        //  377
  "Controls",                                           //  378
  "CorneaEyeRoughness",                                 //  379
  "CorneaSpecularity",                                  //  380
  "Coverage",                                           //  381
  "Culled",                                             //  382
  "Curve",                                              //  383
  "CurveType",                                          //  384
  "DBID",                                               //  385
  "DEPRECATEDTerrainBlendGradientFactor",               //  386
  "DEPRECATEDTerrainBlendStrength",                     //  387
  "DecalSize",                                          //  388
  "DefaultValue",                                       //  389
  "DefineOverridesMap",                                 //  390
  "Defines",                                            //  391
  "DelayUntilAllTextureDependenciesReady",              //  392
  "Density",                                            //  393
  "DensityDistanceExponent",                            //  394
  "DensityFullDistance",                                //  395
  "DensityNoiseBias",                                   //  396
  "DensityNoiseScale",                                  //  397
  "DensityStartDistance",                               //  398
  "DepolarizationFactor",                               //  399
  "DepthBiasInUlp",                                     //  400
  "DepthMVFixup",                                       //  401
  "DepthMVFixupEdgesOnly",                              //  402
  "DepthOfField",                                       //  403
  "DepthOffsetMaskVertexColorChannel",                  //  404
  "DepthScale",                                         //  405
  "DesiredLODLevelCount",                               //  406
  "DetailBlendMask",                                    //  407
  "DetailBlendMaskUVStream",                            //  408
  "DetailBlenderSettings",                              //  409
  "DiffuseTransmissionScale",                           //  410
  "DigitMask",                                          //  411
  "Dir",                                                //  412
  "DirectTransmissionScale",                            //  413
  "Direction",                                          //  414
  "DirectionalColor",                                   //  415
  "DirectionalIlluminance",                             //  416
  "DirectionalLightIlluminanceOverride",                //  417
  "DirectionalityIntensity",                            //  418
  "DirectionalitySaturation",                           //  419
  "DirectionalityScale",                                //  420
  "DisableMipBiasHint",                                 //  421
  "DisableSimulatedVisibility",                         //  422
  "DisplacementMidpoint",                               //  423
  "DisplayName",                                        //  424
  "DisplayTypeName",                                    //  425
  "Dissolve",                                           //  426
  "DistanceKm",                                         //  427
  "DistantLODs",                                        //  428
  "DistantLighting",                                    //  429
  "DitherDistanceMax",                                  //  430
  "DitherDistanceMin",                                  //  431
  "DitherScale",                                        //  432
  "DoubleVisionStrengthValue",                          //  433
  "DownStartValue",                                     //  434
  "Duration",                                           //  435
  "DuskDawnPreset",                                     //  436
  "DynamicState",                                       //  437
  "Easing",                                             //  438
  "Edge",                                               //  439
  "EdgeEffectCoefficient",                              //  440
  "EdgeEffectExponent",                                 //  441
  "EdgeFalloffEnd",                                     //  442
  "EdgeFalloffStart",                                   //  443
  "EdgeMaskContrast",                                   //  444
  "EdgeMaskDistanceMax",                                //  445
  "EdgeMaskDistanceMin",                                //  446
  "EdgeMaskMin",                                        //  447
  "Edges",                                              //  448
  "EffectLighting",                                     //  449
  "EffectSequenceMap",                                  //  450
  "EffectSequenceMapMetadata",                          //  451
  "EffectSequenceMetadataID",                           //  452
  "EffectSequenceObjectMetadata",                       //  453
  "ElevationKm",                                        //  454
  "EmissiveClipThreshold",                              //  455
  "EmissiveMaskSourceBlender",                          //  456
  "EmissiveOnlyAutomaticallyApplied",                   //  457
  "EmissiveOnlyEffect",                                 //  458
  "EmissivePaletteTex",                                 //  459
  "EmissiveSourceLayer",                                //  460
  "EmissiveTint",                                       //  461
  "Emittance",                                          //  462
  "EnableAdaptiveLimits",                               //  463
  "EnableCompensationCurve",                            //  464
  "Enabled",                                            //  465
  "End",                                                //  466
  "EndVisualEffect",                                    //  467
  "Entries",                                            //  468
  "EventName",                                          //  469
  "Events",                                             //  470
  "Exposure",                                           //  471
  "ExposureCompensationCurve",                          //  472
  "ExposureMax",                                        //  473
  "ExposureMin",                                        //  474
  "ExposureOffset",                                     //  475
  "Ext",                                                //  476
  "Exterior",                                           //  477
  "ExteriorAndInterior",                                //  478
  "ExternalColorBindingName",                           //  479
  "ExternalLuminanceBindingName",                       //  480
  "FPS",                                                //  481
  "FadeColorValue",                                     //  482
  "FadeDistanceKm",                                     //  483
  "FadeStartKm",                                        //  484
  "FallbackToRoot",                                     //  485
  "FalloffStartAngle",                                  //  486
  "FalloffStartAngles",                                 //  487
  "FalloffStartOpacities",                              //  488
  "FalloffStartOpacity",                                //  489
  "FalloffStopAngle",                                   //  490
  "FalloffStopAngles",                                  //  491
  "FalloffStopOpacities",                               //  492
  "FalloffStopOpacity",                                 //  493
  "FarFadeValue",                                       //  494
  "FarOpacityValue",                                    //  495
  "FarPlaneValue",                                      //  496
  "FarStartValue",                                      //  497
  "File",                                               //  498
  "FileName",                                           //  499
  "First",                                              //  500
  "FirstBlenderIndex",                                  //  501
  "FirstBlenderMode",                                   //  502
  "FirstLayerIndex",                                    //  503
  "FirstLayerMaskIndex",                                //  504
  "FirstLayerTint",                                     //  505
  "FixedValue",                                         //  506
  "FlipBackFaceNormalsInViewSpace",                     //  507
  "FlowExtent",                                         //  508
  "FlowIsAnimated",                                     //  509
  "FlowMap",                                            //  510
  "FlowMapAndTexturesAreFlipbooks",                     //  511
  "FlowSourceUVChannel",                                //  512
  "FlowSpeed",                                          //  513
  "FlowUVOffset",                                       //  514
  "FlowUVScale",                                        //  515
  "FoamTextureDistortion",                              //  516
  "FoamTextureScrollSpeed",                             //  517
  "Fog",                                                //  518
  "FogDensity",                                         //  519
  "FogFar",                                             //  520
  "FogFarHigh",                                         //  521
  "FogMap",                                             //  522
  "FogMapContribution",                                 //  523
  "FogNear",                                            //  524
  "FogNearHigh",                                        //  525
  "FogScaleValue",                                      //  526
  "FogThickness",                                       //  527
  "Foliage",                                            //  528
  "ForceRenderBeforeClouds",                            //  529
  "ForceRenderBeforeOIT",                               //  530
  "ForceStopOnDetach",                                  //  531
  "ForceStopSound",                                     //  532
  "FormFolderPath",                                     //  533
  "FrequencyMultiplier",                                //  534
  "Frosting",                                           //  535
  "FrostingBlurBias",                                   //  536
  "FrostingUnblurredBackgroundAlphaBlend",              //  537
  "GeoShareBehavior",                                   //  538
  "GlareColor",                                         //  539
  "GlobalLayerNoiseData",                               //  540
  "GreenChannel",                                       //  541
  "HDAComponentIdx",                                    //  542
  "HDAFilePath",                                        //  543
  "HDAVersion",                                         //  544
  "HableShoulderAngle",                                 //  545
  "HableShoulderLength",                                //  546
  "HableShoulderStrength",                              //  547
  "HableToeLength",                                     //  548
  "HableToeStrength",                                   //  549
  "HasData",                                            //  550
  "HasErrors",                                          //  551
  "HasOpacity",                                         //  552
  "HashMap",                                            //  553
  "Height",                                             //  554
  "HeightAboveTerrain",                                 //  555
  "HeightBlendFactor",                                  //  556
  "HeightBlendThreshold",                               //  557
  "HeightFalloffExponent",                              //  558
  "HeightKm",                                           //  559
  "HelpText",                                           //  560
  "HighFrequencyNoiseDensityScale",                     //  561
  "HighFrequencyNoiseScale",                            //  562
  "HighGUID",                                           //  563
  "HorizonFog",                                         //  564
  "HorizonScatteringBlendScale",                        //  565
  "HorizontalAngle",                                    //  566
  "HoudiniDataComponent",                               //  567
  "HurstExponent",                                      //  568
  "ID",                                                 //  569
  "ISO",                                                //  570
  "Id",                                                 //  571
  "IgnoreSmallHeadparts",                               //  572
  "IgnoreWeapons",                                      //  573
  "IgnoredBrightsPercentile",                           //  574
  "IgnoredDarksPercentile",                             //  575
  "IgnoresFog",                                         //  576
  "ImageSpaceSettings",                                 //  577
  "ImageSpaceSettings::AmbientOcclusionSettings",       //  578
  "ImageSpaceSettings::BloomSettings",                  //  579
  "ImageSpaceSettings::BlurSettings",                   //  580
  "ImageSpaceSettings::CinematicSettings",              //  581
  "ImageSpaceSettings::DepthOfFieldSettings",           //  582
  "ImageSpaceSettings::ExposureSettings",               //  583
  "ImageSpaceSettings::ExposureSettings::AutoExposureSettings", //  584
  "ImageSpaceSettings::ExposureSettings::CameraExposureSettings", //  585
  "ImageSpaceSettings::ExposureSettings::LuminanceHistogramSettings", //  586
  "ImageSpaceSettings::FogSettings",                    //  587
  "ImageSpaceSettings::IndirectLightingSettings",       //  588
  "ImageSpaceSettings::RadialBlurSettings",             //  589
  "ImageSpaceSettings::SunAndSkySettings",              //  590
  "ImageSpaceSettings::ToneMappingSettings",            //  591
  "ImageSpaceSettings::VolumetricLightingSettings",     //  592
  "Index",                                              //  593
  "IndirectDiffuseMultiplier",                          //  594
  "IndirectLighting",                                   //  595
  "IndirectLightingSkyScaleOverride",                   //  596
  "IndirectLightingSkyTargetEv100",                     //  597
  "IndirectLightingSkyTargetStrength",                  //  598
  "IndirectSpecRoughness",                              //  599
  "IndirectSpecularMultiplier",                         //  600
  "IndirectSpecularScale",                              //  601
  "IndirectSpecularTransmissionScale",                  //  602
  "InitialAngularVelocity",                             //  603
  "InitialAngularVelocityNoise",                        //  604
  "InitialLinearVelocity",                              //  605
  "InitialLinearVelocityNoise",                         //  606
  "InitialOffset",                                      //  607
  "InnerFadeDistance",                                  //  608
  "InorganicResources",                                 //  609
  "Input",                                              //  610
  "InputDistance",                                      //  611
  "Instances",                                          //  612
  "IntensityValue",                                     //  613
  "InterpretAs",                                        //  614
  "Interval",                                           //  615
  "IrisDepthPosition",                                  //  616
  "IrisDepthTransitionRatio",                           //  617
  "IrisSpecularity",                                    //  618
  "IrisTotalDepth",                                     //  619
  "IrisUVSize",                                         //  620
  "IsAFlipbook",                                        //  621
  "IsAlphaTested",                                      //  622
  "IsDecal",                                            //  623
  "IsDetailBlendMaskSupported",                         //  624
  "IsEmpty",                                            //  625
  "IsGlass",                                            //  626
  "IsMembrane",                                         //  627
  "IsPermanent",                                        //  628
  "IsPlanet",                                           //  629
  "IsProjected",                                        //  630
  "IsSampleInterpolating",                              //  631
  "IsSpikyHair",                                        //  632
  "IsTeeth",                                            //  633
  "Key",                                                //  634
  "KeyMaterialPathForm",                                //  635
  "Keywords",                                           //  636
  "KillSound",                                          //  637
  "LODMeshOverrides",                                   //  638
  "Label",                                              //  639
  "Layer",                                              //  640
  "LayerIndex",                                         //  641
  "Layers",                                             //  642
  "LeafAmplitude",                                      //  643
  "LeafFrequency",                                      //  644
  "LeafSecondaryMotionAmount",                          //  645
  "LeafSecondaryMotionCutOff",                          //  646
  "LensFlareAttachmentComponent",                       //  647
  "LensFlareAttachmentDefine",                          //  648
  "LensFlareCloudOcclusionStrength",                    //  649
  "LensFlareDefines",                                   //  650
  "LightAttachmentConfiguration",                       //  651
  "LightAttachmentDefine",                              //  652
  "LightAttachmentFormComponent",                       //  653
  "LightAttachmentRef",                                 //  654
  "LightLuminanceMultiplier",                           //  655
  "LightingPower",                                      //  656
  "LightingWrap",                                       //  657
  "LightningColor",                                     //  658
  "LightningDistanceMax",                               //  659
  "LightningDistanceMin",                               //  660
  "LightningFieldOfView",                               //  661
  "LightningStrikeEffect",                              //  662
  "Loop",                                               //  663
  "LoopSegment",                                        //  664
  "Loops",                                              //  665
  "LowGUID",                                            //  666
  "LowLOD",                                             //  667
  "LowLODRootMaterial",                                 //  668
  "LuminanceAtTime",                                    //  669
  "LuminanceHistogram",                                 //  670
  "LuminousEmittance",                                  //  671
  "Map",                                                //  672
  "MapMetadata",                                        //  673
  "MappingsA",                                          //  674
  "Mask",                                               //  675
  "MaskDistanceFromShoreEnd",                           //  676
  "MaskDistanceFromShoreStart",                         //  677
  "MaskDistanceRampWidth",                              //  678
  "MaskIntensityMax",                                   //  679
  "MaskIntensityMin",                                   //  680
  "MaskNoiseAmp",                                       //  681
  "MaskNoiseAnimSpeed",                                 //  682
  "MaskNoiseBias",                                      //  683
  "MaskNoiseFreq",                                      //  684
  "MaskNoiseGlobalScale",                               //  685
  "MaskWaveParallax",                                   //  686
  "Material",                                           //  687
  "MaterialMaskIntensityScale",                         //  688
  "MaterialOverallAlpha",                               //  689
  "MaterialPath",                                       //  690
  "MaterialProperty",                                   //  691
  "MaterialTypeOverride",                               //  692
  "Materials",                                          //  693
  "Max",                                                //  694
  "MaxConcentrationPlankton",                           //  695
  "MaxConcentrationSediment",                           //  696
  "MaxConcentrationYellowMatter",                       //  697
  "MaxDelay",                                           //  698
  "MaxDepthOffset",                                     //  699
  "MaxDisplacement",                                    //  700
  "MaxFogDensity",                                      //  701
  "MaxFogThickness",                                    //  702
  "MaxIndex",                                           //  703
  "MaxInput",                                           //  704
  "MaxMeanFreePath",                                    //  705
  "MaxOffsetEmittance",                                 //  706
  "MaxParralaxOcclusionSteps",                          //  707
  "MaxValue",                                           //  708
  "MeanFreePath",                                       //  709
  "MediumLODRootMaterial",                              //  710
  "MeshFileOverride",                                   //  711
  "MeshLODDistanceOverride",                            //  712
  "MeshLODs",                                           //  713
  "MeshOverrides",                                      //  714
  "Metadata",                                           //  715
  "Mie",                                                //  716
  "MieCoef",                                            //  717
  "Min",                                                //  718
  "MinDelay",                                           //  719
  "MinFogDensity",                                      //  720
  "MinFogThickness",                                    //  721
  "MinIndex",                                           //  722
  "MinInput",                                           //  723
  "MinMeanFreePath",                                    //  724
  "MinOffsetEmittance",                                 //  725
  "MinValue",                                           //  726
  "MipBase",                                            //  727
  "Misc",                                               //  728
  "Mode",                                               //  729
  "MoleculesPerUnitVolume",                             //  730
  "MoonGlare",                                          //  731
  "Moonlight",                                          //  732
  "MostSignificantLayer",                               //  733
  "MotionBlurStrengthValue",                            //  734
  "Name",                                               //  735
  "NavMeshAreaFlag",                                    //  736
  "NavMeshSplineExtraData::ChunkData",                  //  737
  "NavMeshSplineExtraData::ChunkDataRef",               //  738
  "NearFadeValue",                                      //  739
  "NearOpacityValue",                                   //  740
  "NearPlaneValue",                                     //  741
  "NearStartValue",                                     //  742
  "NestedTracks",                                       //  743
  "NiPoint3",                                           //  744
  "NightPreset",                                        //  745
  "NoHalfResOptimization",                              //  746
  "NoLAngularDamping",                                  //  747
  "NoLinearDamping",                                    //  748
  "NoSky",                                              //  749
  "Node",                                               //  750
  "NodeAttachmentConfiguration",                        //  751
  "NodeName",                                           //  752
  "Nodes",                                              //  753
  "NoiseBias",                                          //  754
  "NoiseContribution",                                  //  755
  "NoiseMaskTexture",                                   //  756
  "NoiseScale",                                         //  757
  "NoiseScrollingVelocity",                             //  758
  "NormalAffectsStrength",                              //  759
  "NormalOverride",                                     //  760
  "NormalShadowStrength",                               //  761
  "NormalTexture",                                      //  762
  "NumLODMaterials",                                    //  763
  "Object",                                             //  764
  "ObjectAttachmentConfiguration",                      //  765
  "ObjectID",                                           //  766
  "Objects",                                            //  767
  "Offset",                                             //  768
  "Op",                                                 //  769
  "OpacitySourceLayer",                                 //  770
  "OpacityTexture",                                     //  771
  "OpacityUVStream",                                    //  772
  "Optimized",                                          //  773
  "Order",                                              //  774
  "OuterFadeDistance",                                  //  775
  "OverallBlendAmount",                                 //  776
  "OverrideAttachType",                                 //  777
  "OverrideInitialVelocities",                          //  778
  "OverrideLevelCount",                                 //  779
  "OverrideMaterial",                                   //  780
  "OverrideMaterialPath",                               //  781
  "OverrideMeshLODDistance",                            //  782
  "OverrideSearchMethod",                               //  783
  "OverrideToneMapping",                                //  784
  "Overrides",                                          //  785
  "OzoneAbsorptionCoef",                                //  786
  "ParallaxOcclusionScale",                             //  787
  "ParallaxOcclusionShadows",                           //  788
  "ParamIndex",                                         //  789
  "ParameterName",                                      //  790
  "ParametersA",                                        //  791
  "Parent",                                             //  792
  "ParentPersistentID",                                 //  793
  "ParticleAttachmentConfiguration",                    //  794
  "ParticleFormComponent",                              //  795
  "ParticleRenderState",                                //  796
  "ParticleSystemDefine",                               //  797
  "ParticleSystemDefineOverrides",                      //  798
  "ParticleSystemDefineRef",                            //  799
  "ParticleSystemPath",                                 //  800
  "ParticleSystemReferenceDefine",                      //  801
  "Path",                                               //  802
  "PathStr",                                            //  803
  "PersistentID",                                       //  804
  "PhytoplanktonReflectanceColorB",                     //  805
  "PhytoplanktonReflectanceColorG",                     //  806
  "PhytoplanktonReflectanceColorR",                     //  807
  "PlacedWater",                                        //  808
  "Planes",                                             //  809
  "PlanetStarGlowBackgroundScale",                      //  810
  "PlanetStarfieldBackgroundGIContribution",            //  811
  "PlanetStarfieldBackgroundScale",                     //  812
  "PlanetStarfieldStarBrightnessScale",                 //  813
  "PlayMode",                                           //  814
  "PlayOnCulledNodes",                                  //  815
  "PlayerHUDEffect",                                    //  816
  "Position",                                           //  817
  "PositionOffset",                                     //  818
  "PositionOffsetEnabled",                              //  819
  "PrecipFadeIn",                                       //  820
  "PrecipFadeOut",                                      //  821
  "Precipitation",                                      //  822
  "PreferREFR",                                         //  823
  "PreferREFREnabled",                                  //  824
  "PresetData",                                         //  825
  "PreviewModelPath",                                   //  826
  "PrewarmTime",                                        //  827
  "ProjectedDecalSetting",                              //  828
  "Properties",                                         //  829
  "Property",                                           //  830
  "RadialBlur",                                         //  831
  "RampDownValue",                                      //  832
  "RampUpValue",                                        //  833
  "RandomPlaySpeedRange",                               //  834
  "RandomTimeOffset",                                   //  835
  "Range",                                              //  836
  "RaycastDirection",                                   //  837
  "RaycastLength",                                      //  838
  "Rayleigh",                                           //  839
  "RayleighCoef",                                       //  840
  "ReceiveDirectionalShadows",                          //  841
  "ReceiveNonDirectionalShadows",                       //  842
  "ReciprocalTickRate",                                 //  843
  "RedChannel",                                         //  844
  "RefID",                                              //  845
  "ReferenceDefines",                                   //  846
  "References",                                         //  847
  "ReflectanceB",                                       //  848
  "ReflectanceG",                                       //  849
  "ReflectanceR",                                       //  850
  "ReflectionProbeCellComponent",                       //  851
  "ReflectionProbeInstanceData",                        //  852
  "RefractiveIndexOfAir",                               //  853
  "RemapAlbedo",                                        //  854
  "RemapEmissive",                                      //  855
  "RemapOpacity",                                       //  856
  "RenderLayer",                                        //  857
  "Replacement",                                        //  858
  "RequiresNodeType",                                   //  859
  "ResetOnEnd",                                         //  860
  "ResetOnLoop",                                        //  861
  "ResolutionHint",                                     //  862
  "RespectSceneTransform",                              //  863
  "RevertMaterialOnSequenceEnd",                        //  864
  "RotationAngle",                                      //  865
  "RotationOffset",                                     //  866
  "RotationOffsetEnabled",                              //  867
  "Roughness",                                          //  868
  "RoundToNearest",                                     //  869
  "Route",                                              //  870
  "Rows",                                               //  871
  "SSSStrength",                                        //  872
  "SSSWidth",                                           //  873
  "Saturation",                                         //  874
  "ScaleOffset",                                        //  875
  "ScaleOffsetEnabled",                                 //  876
  "Scattering",                                         //  877
  "ScatteringFar",                                      //  878
  "ScatteringScale",                                    //  879
  "ScatteringTransition",                               //  880
  "ScatteringVolumeFar",                                //  881
  "ScatteringVolumeNear",                               //  882
  "ScleraEyeRoughness",                                 //  883
  "ScleraSpecularity",                                  //  884
  "SearchFromTopLevelFadeNode",                         //  885
  "SearchMethod",                                       //  886
  "Second",                                             //  887
  "SecondBlenderIndex",                                 //  888
  "SecondBlenderMode",                                  //  889
  "SecondLayerActive",                                  //  890
  "SecondLayerIndex",                                   //  891
  "SecondLayerMaskIndex",                               //  892
  "SecondLayerTint",                                    //  893
  "SecondMostSignificantLayer",                         //  894
  "SedimentReflectanceColorB",                          //  895
  "SedimentReflectanceColorG",                          //  896
  "SedimentReflectanceColorR",                          //  897
  "SequenceID",                                         //  898
  "SequenceName",                                       //  899
  "Sequences",                                          //  900
  "Settings",                                           //  901
  "SettingsTemplate",                                   //  902
  "Shadows",                                            //  903
  "ShakeMultiplier",                                    //  904
  "ShakeType",                                          //  905
  "ShouldRescale",                                      //  906
  "SkyLightingMultiplier",                              //  907
  "SoftEffect",                                         //  908
  "SoftFalloffDepth",                                   //  909
  "Sound",                                              //  910
  "SoundEffects",                                       //  911
  "SoundHook",                                          //  912
  "Sounds",                                             //  913
  "SourceDirection",                                    //  914
  "SourceDirectoryHash",                                //  915
  "SourceID",                                           //  916
  "SpaceGlowBackgroundScaleOverride",                   //  917
  "Span",                                               //  918
  "SpawnMethod",                                        //  919
  "SpecLobe0RoughnessScale",                            //  920
  "SpecLobe1RoughnessScale",                            //  921
  "SpecScale",                                          //  922
  "SpecularOpacityOverride",                            //  923
  "SpecularTransmissionScale",                          //  924
  "Speed",                                              //  925
  "SpellData",                                          //  926
  "SpellItems",                                         //  927
  "SplineColor_Packed",                                 //  928
  "SplineSide",                                         //  929
  "StarfieldBackgroundScaleOverride",                   //  930
  "StarfieldStarBrightnessScaleOverride",               //  931
  "Stars",                                              //  932
  "Start",                                              //  933
  "StartEvent",                                         //  934
  "StartTargetIndex",                                   //  935
  "StartValue",                                         //  936
  "StaticVisibility",                                   //  937
  "StopEvent",                                          //  938
  "StreamID",                                           //  939
  "Strength",                                           //  940
  "StrengthValue",                                      //  941
  "SubWeathers",                                        //  942
  "Sun",                                                //  943
  "SunAndSky",                                          //  944
  "SunColor",                                           //  945
  "SunDiskIlluminanceScaleOverride",                    //  946
  "SunDiskIndirectIlluminanceScaleOverride",            //  947
  "SunDiskScreenSizeMax",                               //  948
  "SunDiskScreenSizeMin",                               //  949
  "SunDiskTexture",                                     //  950
  "SunGlare",                                           //  951
  "SunGlareColor",                                      //  952
  "SunIlluminance",                                     //  953
  "Sunlight",                                           //  954
  "SurfaceHeightMap",                                   //  955
  "SwapsCollection",                                    //  956
  "TESImageSpace",                                      //  957
  "TESImageSpaceModifier",                              //  958
  "Tangent",                                            //  959
  "TangentBend",                                        //  960
  "TargetID",                                           //  961
  "TargetUVStream",                                     //  962
  "TerrainBlendGradientFactor",                         //  963
  "TerrainBlendStrength",                               //  964
  "TerrainMatch",                                       //  965
  "TexcoordScaleAndBias",                               //  966
  "TexcoordScale_XY",                                   //  967
  "TexcoordScale_XZ",                                   //  968
  "TexcoordScale_YZ",                                   //  969
  "Texture",                                            //  970
  "TextureMappingType",                                 //  971
  "TextureShadowOffset",                                //  972
  "TextureShadowStrength",                              //  973
  "Thickness",                                          //  974
  "ThicknessNoiseBias",                                 //  975
  "ThicknessNoiseScale",                                //  976
  "ThicknessTexture",                                   //  977
  "Thin",                                               //  978
  "ThirdLayerActive",                                   //  979
  "ThirdLayerIndex",                                    //  980
  "ThirdLayerMaskIndex",                                //  981
  "ThirdLayerTint",                                     //  982
  "Threshold",                                          //  983
  "ThunderFadeIn",                                      //  984
  "ThunderFadeOut",                                     //  985
  "TicksPerSecond",                                     //  986
  "Tiling",                                             //  987
  "TilingDistance",                                     //  988
  "TilingPerKm",                                        //  989
  "Time",                                               //  990
  "TimeMultiplier",                                     //  991
  "TimelineStartTime",                                  //  992
  "TimelineUnitsPerPixel",                              //  993
  "Tint",                                               //  994
  "TintColorValue",                                     //  995
  "ToneMapE",                                           //  996
  "ToneMapping",                                        //  997
  "TopBlendDistanceKm",                                 //  998
  "TopBlendStartKm",                                    //  999
  "Tracks",                                             // 1000
  "TransDelta",                                         // 1001
  "TransformHandling",                                  // 1002
  "TransformMode",                                      // 1003
  "TransitionEndAngle",                                 // 1004
  "TransitionStartAngle",                               // 1005
  "TransitionThreshold",                                // 1006
  "TransmissiveScale",                                  // 1007
  "TransmittanceSourceLayer",                           // 1008
  "TransmittanceWidth",                                 // 1009
  "TrunkFlexibility",                                   // 1010
  "TurbulenceDirectionAmplitude",                       // 1011
  "TurbulenceDirectionFrequency",                       // 1012
  "TurbulenceSpeedAmplitude",                           // 1013
  "TurbulenceSpeedFrequency",                           // 1014
  "Type",                                               // 1015
  "UVStreamTargetBlender",                              // 1016
  "UVStreamTargetLayer",                                // 1017
  "UniqueID",                                           // 1018
  "UseAsInteriorCriteria",                              // 1019
  "UseBoundsToScaleDissolve",                           // 1020
  "UseCustomCoefficients",                              // 1021
  "UseDetailBlendMask",                                 // 1022
  "UseDigitMask",                                       // 1023
  "UseDitheredTransparency",                            // 1024
  "UseFallOff",                                         // 1025
  "UseGBufferNormals",                                  // 1026
  "UseNodeLocalRotation",                               // 1027
  "UseNoiseMaskTexture",                                // 1028
  "UseOutdoorExposure",                                 // 1029
  "UseOutdoorLUT",                                      // 1030
  "UseOzoneAbsorptionApproximation",                    // 1031
  "UseParallaxOcclusionMapping",                        // 1032
  "UseRGBFallOff",                                      // 1033
  "UseRandomOffset",                                    // 1034
  "UseSSS",                                             // 1035
  "UseStartTargetIndex",                                // 1036
  "UseTargetForDepthOfField",                           // 1037
  "UseTargetForRadialBlur",                             // 1038
  "UseVertexAlpha",                                     // 1039
  "UseVertexColor",                                     // 1040
  "UseWorldAlignedRaycastDirection",                    // 1041
  "UserDuration",                                       // 1042
  "UsesDirectionality",                                 // 1043
  "Value",                                              // 1044
  "Variables",                                          // 1045
  "VariationStrength",                                  // 1046
  "Version",                                            // 1047
  "VertexColorBlend",                                   // 1048
  "VertexColorChannel",                                 // 1049
  "VerticalAngle",                                      // 1050
  "VerticalTiling",                                     // 1051
  "VeryLowLODRootMaterial",                             // 1052
  "VisibilityMultiplier",                               // 1053
  "Visible",                                            // 1054
  "VolatilityMultiplier",                               // 1055
  "VolumetricIndirectLightContribution",                // 1056
  "VolumetricLighting",                                 // 1057
  "VolumetricLightingDirectionalAnisoScale",            // 1058
  "VolumetricLightingDirectionalLightScale",            // 1059
  "WarmUp",                                             // 1060
  "WaterDepthBlur",                                     // 1061
  "WaterEdgeFalloff",                                   // 1062
  "WaterEdgeNormalFalloff",                             // 1063
  "WaterIndirectSpecularMultiplier",                    // 1064
  "WaterRefractionMagnitude",                           // 1065
  "WaterWetnessMaxDepth",                               // 1066
  "WaveAmplitude",                                      // 1067
  "WaveDistortionAmount",                               // 1068
  "WaveFlipWaveDirection",                              // 1069
  "WaveParallaxFalloffBias",                            // 1070
  "WaveParallaxFalloffScale",                           // 1071
  "WaveParallaxInnerStrength",                          // 1072
  "WaveParallaxOuterStrength",                          // 1073
  "WaveScale",                                          // 1074
  "WaveShoreFadeInnerDistance",                         // 1075
  "WaveShoreFadeOuterDistance",                         // 1076
  "WaveSpawnFadeInDistance",                            // 1077
  "WaveSpeed",                                          // 1078
  "WeatherActivateEffect",                              // 1079
  "WeatherChoice",                                      // 1080
  "Weight",                                             // 1081
  "WeightMultiplier",                                   // 1082
  "Weighted",                                           // 1083
  "WhitePointValue",                                    // 1084
  "WindDirectionOverrideEnabled",                       // 1085
  "WindDirectionOverrideValue",                         // 1086
  "WindDirectionRange",                                 // 1087
  "WindScale",                                          // 1088
  "WindStrengthVariationMax",                           // 1089
  "WindStrengthVariationMin",                           // 1090
  "WindStrengthVariationSpeed",                         // 1091
  "WindTurbulence",                                     // 1092
  "WorldspaceScaleFactor",                              // 1093
  "WriteMask",                                          // 1094
  "XCurve",                                             // 1095
  "XMCOLOR",                                            // 1096
  "XMFLOAT2",                                           // 1097
  "XMFLOAT3",                                           // 1098
  "XMFLOAT4",                                           // 1099
  "YCurve",                                             // 1100
  "YellowMatterReflectanceColorB",                      // 1101
  "YellowMatterReflectanceColorG",                      // 1102
  "YellowMatterReflectanceColorR",                      // 1103
  "ZCurve",                                             // 1104
  "ZTest",                                              // 1105
  "ZWrite",                                             // 1106
  "a",                                                  // 1107
  "b",                                                  // 1108
  "g",                                                  // 1109
  "pArtForm",                                           // 1110
  "pClimateOverride",                                   // 1111
  "pCloudCardSequence",                                 // 1112
  "pClouds",                                            // 1113
  "pConditionForm",                                     // 1114
  "pConditions",                                        // 1115
  "pDefineCollection",                                  // 1116
  "pDisplayNameKeyword",                                // 1117
  "pEventForm",                                         // 1118
  "pExplosionForm",                                     // 1119
  "pExternalForm",                                      // 1120
  "pFalloff",                                           // 1121
  "pFormReference",                                     // 1122
  "pImageSpace",                                        // 1123
  "pImageSpaceDay",                                     // 1124
  "pImageSpaceForm",                                    // 1125
  "pImageSpaceNight",                                   // 1126
  "pImpactDataSet",                                     // 1127
  "pLensFlare",                                         // 1128
  "pLightForm",                                         // 1129
  "pLightningFX",                                       // 1130
  "pMainSpell",                                         // 1131
  "pOptionalPhotoModeEffect",                           // 1132
  "pParent",                                            // 1133
  "pParentForm",                                        // 1134
  "pPrecipitationEffect",                               // 1135
  "pPreviewForm",                                       // 1136
  "pProjectedDecalForm",                                // 1137
  "pSource",                                            // 1138
  "pSpell",                                             // 1139
  "pSunPresetOverride",                                 // 1140
  "pTimeOfDayData",                                     // 1141
  "pTransitionSpell",                                   // 1142
  "pVisualEffect",                                      // 1143
  "pVolumeticLighting",                                 // 1144
  "pWindForce",                                         // 1145
  "r",                                                  // 1146
  "spController",                                       // 1147
  "upControllers",                                      // 1148
  "upDir",                                              // 1149
  "upMap",                                              // 1150
  "upObjectToAttach",                                   // 1151
  "upObjectToSpawn",                                    // 1152
  "w",                                                  // 1153
  "x",                                                  // 1154
  "y",                                                  // 1155
  "z"                                                   // 1156
};

