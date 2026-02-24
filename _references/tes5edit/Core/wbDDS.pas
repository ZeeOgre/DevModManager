{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit wbDDS;

interface

uses
  System.SysUtils,
  System.Classes;

type
  TMagic4 = array [0..3] of AnsiChar;
  PMagic4 = ^TMagic4;

  TDXGI = (
    DXGI_FORMAT_UNKNOWN,
    DXGI_FORMAT_R32G32B32A32_TYPELESS,
    DXGI_FORMAT_R32G32B32A32_FLOAT,
    DXGI_FORMAT_R32G32B32A32_UINT,
    DXGI_FORMAT_R32G32B32A32_SINT,
    DXGI_FORMAT_R32G32B32_TYPELESS,
    DXGI_FORMAT_R32G32B32_FLOAT,
    DXGI_FORMAT_R32G32B32_UINT,
    DXGI_FORMAT_R32G32B32_SINT,
    DXGI_FORMAT_R16G16B16A16_TYPELESS,
    DXGI_FORMAT_R16G16B16A16_FLOAT,
    DXGI_FORMAT_R16G16B16A16_UNORM,
    DXGI_FORMAT_R16G16B16A16_UINT,
    DXGI_FORMAT_R16G16B16A16_SNORM,
    DXGI_FORMAT_R16G16B16A16_SINT,
    DXGI_FORMAT_R32G32_TYPELESS,
    DXGI_FORMAT_R32G32_FLOAT,
    DXGI_FORMAT_R32G32_UINT,
    DXGI_FORMAT_R32G32_SINT,
    DXGI_FORMAT_R32G8X24_TYPELESS,
    DXGI_FORMAT_D32_FLOAT_S8X24_UINT,
    DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS,
    DXGI_FORMAT_X32_TYPELESS_G8X24_UINT,
    DXGI_FORMAT_R10G10B10A2_TYPELESS,
    DXGI_FORMAT_R10G10B10A2_UNORM,
    DXGI_FORMAT_R10G10B10A2_UINT,
    DXGI_FORMAT_R11G11B10_FLOAT,
    DXGI_FORMAT_R8G8B8A8_TYPELESS,
    DXGI_FORMAT_R8G8B8A8_UNORM,
    DXGI_FORMAT_R8G8B8A8_UNORM_SRGB,
    DXGI_FORMAT_R8G8B8A8_UINT,
    DXGI_FORMAT_R8G8B8A8_SNORM,
    DXGI_FORMAT_R8G8B8A8_SINT,
    DXGI_FORMAT_R16G16_TYPELESS,
    DXGI_FORMAT_R16G16_FLOAT,
    DXGI_FORMAT_R16G16_UNORM,
    DXGI_FORMAT_R16G16_UINT,
    DXGI_FORMAT_R16G16_SNORM,
    DXGI_FORMAT_R16G16_SINT,
    DXGI_FORMAT_R32_TYPELESS,
    DXGI_FORMAT_D32_FLOAT,
    DXGI_FORMAT_R32_FLOAT,
    DXGI_FORMAT_R32_UINT,
    DXGI_FORMAT_R32_SINT,
    DXGI_FORMAT_R24G8_TYPELESS,
    DXGI_FORMAT_D24_UNORM_S8_UINT,
    DXGI_FORMAT_R24_UNORM_X8_TYPELESS,
    DXGI_FORMAT_X24_TYPELESS_G8_UINT,
    DXGI_FORMAT_R8G8_TYPELESS,
    DXGI_FORMAT_R8G8_UNORM,
    DXGI_FORMAT_R8G8_UINT,
    DXGI_FORMAT_R8G8_SNORM,
    DXGI_FORMAT_R8G8_SINT,
    DXGI_FORMAT_R16_TYPELESS,
    DXGI_FORMAT_R16_FLOAT,
    DXGI_FORMAT_D16_UNORM,
    DXGI_FORMAT_R16_UNORM,
    DXGI_FORMAT_R16_UINT,
    DXGI_FORMAT_R16_SNORM,
    DXGI_FORMAT_R16_SINT,
    DXGI_FORMAT_R8_TYPELESS,
    DXGI_FORMAT_R8_UNORM,
    DXGI_FORMAT_R8_UINT,
    DXGI_FORMAT_R8_SNORM,
    DXGI_FORMAT_R8_SINT,
    DXGI_FORMAT_A8_UNORM,
    DXGI_FORMAT_R1_UNORM,
    DXGI_FORMAT_R9G9B9E5_SHAREDEXP,
    DXGI_FORMAT_R8G8_B8G8_UNORM,
    DXGI_FORMAT_G8R8_G8B8_UNORM,
    DXGI_FORMAT_BC1_TYPELESS,
    DXGI_FORMAT_BC1_UNORM,
    DXGI_FORMAT_BC1_UNORM_SRGB,
    DXGI_FORMAT_BC2_TYPELESS,
    DXGI_FORMAT_BC2_UNORM,
    DXGI_FORMAT_BC2_UNORM_SRGB,
    DXGI_FORMAT_BC3_TYPELESS,
    DXGI_FORMAT_BC3_UNORM,
    DXGI_FORMAT_BC3_UNORM_SRGB,
    DXGI_FORMAT_BC4_TYPELESS,
    DXGI_FORMAT_BC4_UNORM,
    DXGI_FORMAT_BC4_SNORM,
    DXGI_FORMAT_BC5_TYPELESS,
    DXGI_FORMAT_BC5_UNORM,
    DXGI_FORMAT_BC5_SNORM,
    DXGI_FORMAT_B5G6R5_UNORM,
    DXGI_FORMAT_B5G5R5A1_UNORM,
    DXGI_FORMAT_B8G8R8A8_UNORM,
    DXGI_FORMAT_B8G8R8X8_UNORM,
    DXGI_FORMAT_R10G10B10_XR_BIAS_A2_UNORM,
    DXGI_FORMAT_B8G8R8A8_TYPELESS,
    DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,
    DXGI_FORMAT_B8G8R8X8_TYPELESS,
    DXGI_FORMAT_B8G8R8X8_UNORM_SRGB,
    DXGI_FORMAT_BC6H_TYPELESS,
    DXGI_FORMAT_BC6H_UF16,
    DXGI_FORMAT_BC6H_SF16,
    DXGI_FORMAT_BC7_TYPELESS,
    DXGI_FORMAT_BC7_UNORM,
    DXGI_FORMAT_BC7_UNORM_SRGB,
    DXGI_FORMAT_AYUV,
    DXGI_FORMAT_Y410,
    DXGI_FORMAT_Y416,
    DXGI_FORMAT_NV12,
    DXGI_FORMAT_P010,
    DXGI_FORMAT_P016,
    DXGI_FORMAT_420_OPAQUE,
    DXGI_FORMAT_YUY2,
    DXGI_FORMAT_Y210,
    DXGI_FORMAT_Y216,
    DXGI_FORMAT_NV11,
    DXGI_FORMAT_AI44,
    DXGI_FORMAT_IA44,
    DXGI_FORMAT_P8,
    DXGI_FORMAT_A8P8,
    DXGI_FORMAT_B4G4R4A4_UNORM,
    DXGI_FORMAT_P208,
    DXGI_FORMAT_V208,
    DXGI_FORMAT_V408
  );
  TDXGIs = set of TDXGI;

  TDDSHeader = packed record
    Magic: TMagic4;
    dwSize: Cardinal;
    dwFlags: Cardinal;
    dwHeight: Cardinal;
    dwWidth: Cardinal;
    dwPitchOrLinearSize: Cardinal;
    dwDepth: Cardinal;
    dwMipMapCount: Cardinal;
    dwReserved1: array [0..10] of Cardinal;
    ddspf: packed record
      dwSize: Cardinal;
      dwFlags: Cardinal;
      dwFourCC: TMagic4;
      dwRGBBitCount: Cardinal;
      dwRBitMask: Cardinal;
      dwGBitMask: Cardinal;
      dwBBitMask: Cardinal;
      dwABitMask: Cardinal;
    end;
    dwCaps: Cardinal;
    dwCaps2: Cardinal;
    dwCaps3: Cardinal;
    dwCaps4: Cardinal;
    dwReserved2: Cardinal;
  end;
  PDDSHeader = ^TDDSHeader;

  TDDSHeaderDX10 = packed record
    dxgiFormat: Integer;
    resourceDimension: Cardinal;
    miscFlags: Cardinal;
    arraySize: Cardinal;
    miscFlags2: Cardinal;
  end;
  PDDSHeaderDX10 = ^TDDSHeaderDX10;

const
  DXGI_DX10: TDXGIs = [
    DXGI_FORMAT_BC1_UNORM_SRGB,
    DXGI_FORMAT_BC2_UNORM_SRGB, DXGI_FORMAT_BC3_UNORM_SRGB,
    DXGI_FORMAT_BC6H_UF16, DXGI_FORMAT_BC6H_SF16,
    DXGI_FORMAT_BC7_UNORM, DXGI_FORMAT_BC7_UNORM_SRGB,
    DXGI_FORMAT_B8G8R8A8_UNORM_SRGB, DXGI_FORMAT_B8G8R8X8_UNORM_SRGB,
    DXGI_FORMAT_R8G8B8A8_SINT, DXGI_FORMAT_R8G8B8A8_UINT, DXGI_FORMAT_R8G8B8A8_UNORM_SRGB,
    DXGI_FORMAT_R8G8_SINT, DXGI_FORMAT_R8G8_UINT,
    DXGI_FORMAT_R8_SINT, DXGI_FORMAT_R8_SNORM, DXGI_FORMAT_R8_UINT
  ];

  MAGIC_DX10: TMagic4 = 'DX10';
  MAGIC_DDS : TMagic4 = 'DDS ';
  MAGIC_DXT1: TMagic4 = 'DXT1';
  MAGIC_DXT3: TMagic4 = 'DXT3';
  MAGIC_DXT5: TMagic4 = 'DXT5';
  MAGIC_ATI1: TMagic4 = 'ATI1';
  MAGIC_ATI2: TMagic4 = 'ATI2';
  MAGIC_BC4S: TMagic4 = 'BC4S';
  MAGIC_BC4U: TMagic4 = 'BC4U';
  MAGIC_BC5S: TMagic4 = 'BC5S';
  MAGIC_BC5U: TMagic4 = 'BC5U';

  DDSD_CAPS        = $00000001;
  DDSD_HEIGHT      = $00000002;
  DDSD_WIDTH       = $00000004;
  DDSD_PITCH       = $00000008;
  DDSD_PIXELFORMAT = $00001000;
  DDSD_MIPMAPCOUNT = $00020000;
  DDSD_LINEARSIZE  = $00080000;
  DDSD_DEPTH       = $00800000;

  DDSCAPS_COMPLEX  = $00000008;
  DDSCAPS_TEXTURE  = $00001000;
  DDSCAPS_MIPMAP   = $00400000;

  DDSCAPS2_CUBEMAP           = $00000200;
  DDSCAPS2_CUBEMAP_POSITIVEX = $00000400;
  DDSCAPS2_CUBEMAP_NEGATIVEX = $00000800;
  DDSCAPS2_CUBEMAP_POSITIVEY = $00001000;
  DDSCAPS2_CUBEMAP_NEGATIVEY = $00002000;
  DDSCAPS2_CUBEMAP_POSITIVEZ = $00004000;
  DDSCAPS2_CUBEMAP_NEGATIVEZ = $00008000;
  DDSCAPS2_CUBEMAP_VOLUME    = $00200000;
  DDSCAPS2_CUBEMAP_ALLFACES  =
    DDSCAPS2_CUBEMAP_POSITIVEX or DDSCAPS2_CUBEMAP_NEGATIVEX or
    DDSCAPS2_CUBEMAP_POSITIVEY or DDSCAPS2_CUBEMAP_NEGATIVEY or
    DDSCAPS2_CUBEMAP_POSITIVEZ or DDSCAPS2_CUBEMAP_NEGATIVEZ;

  DDPF_ALPHAPIXELS = $00000001;
  DDPF_ALPHA       = $00000002;
  DDPF_FOURCC      = $00000004;
  DDPF_RGB         = $00000040;
  DDPF_YUV         = $00000200;
  DDPF_LUMINANCE   = $00020000;

  // DX10
  DDS_DIMENSION_TEXTURE2D       = $00000003;
  DDS_RESOURCE_MISC_TEXTURECUBE = $00000004;


function IsDDS(aDDSData: Pointer): Boolean;
function GetDDSHeaderSize(aDDSData: Pointer): Integer;
function GetDXGIFormatName(aDXGI: TDXGI): string;
function GetDXGI(aDDSData: Pointer): TDXGI;
procedure SetDDSHeader(aDDSData: Pointer; aDXGI: TDXGI;
  aWidth, aHeight, aMipMapCount: Integer; aCubeMap: Boolean);
function GetBitsPerPixel(aDXGI: TDXGI): Byte;
function IsDDSCubeMap(aDDSData: Pointer): Boolean;


implementation

uses
  TypInfo;


function IsDDS(aDDSData: Pointer): Boolean;
begin
  Result := Assigned(aDDSData) and (PDDSHeader(aDDSData).Magic = MAGIC_DDS);
end;

function GetDDSHeaderSize(aDDSData: Pointer): Integer;
var
  DDSHeader: PDDSHeader;
begin
  DDSHeader := aDDSData;
  Result := SizeOf(TDDSHeader);
  if DDSHeader.ddspf.dwFourCC = MAGIC_DX10 then
    Inc(Result, SizeOf(TDDSHeaderDX10));
end;

function GetDXGIFormatName(aDXGI: TDXGI): string;
begin
  Result := GetEnumName(TypeInfo(TDXGI), Integer(aDXGI)).Replace('DXGI_FORMAT_', '') ;
end;

function GetDXGI(aDDSData: Pointer): TDXGI;
var
  DDSHeader: PDDSHeader;
  DDSHeaderDX10: PDDSHeaderDX10;
begin
  DDSHeader := aDDSData;

  with DDSHeader.ddspf do
  if dwFourCC = MAGIC_DXT1 then
    Result := DXGI_FORMAT_BC1_UNORM
  else if dwFourCC = MAGIC_DXT3 then
    Result := DXGI_FORMAT_BC2_UNORM
  else if dwFourCC = MAGIC_DXT5 then
    Result := DXGI_FORMAT_BC3_UNORM
  else if dwFourCC = MAGIC_ATI1 then
    Result := DXGI_FORMAT_BC4_UNORM
  else if dwFourCC = MAGIC_BC4U then
    Result := DXGI_FORMAT_BC4_UNORM
  else if dwFourCC = MAGIC_BC4S then
    Result := DXGI_FORMAT_BC4_SNORM
  else if dwFourCC = MAGIC_ATI2 then
    Result := DXGI_FORMAT_BC5_UNORM
  else if dwFourCC = MAGIC_BC5U then
    Result := DXGI_FORMAT_BC5_UNORM
  else if dwFourCC = MAGIC_BC5S then
    Result := DXGI_FORMAT_BC5_SNORM
  else if dwFourCC = MAGIC_DX10 then begin
    DDSHeaderDX10 := Pointer(PByte(DDSHeader) + SizeOf(DDSHeader^));
    Result := TDXGI(DDSHeaderDX10.dxgiFormat);
  end
  else begin
    if dwRGBBitCount = 32 then
      if dwFlags and DDPF_ALPHAPIXELS = 0 then
        Result := DXGI_FORMAT_B8G8R8X8_UNORM
      else if dwRBitMask = $000000FF then
        Result := DXGI_FORMAT_R8G8B8A8_UNORM
      else
        Result := DXGI_FORMAT_B8G8R8A8_UNORM

    else if dwRGBBitCount = 16 then
      if (dwRBitMask = $F800) and (dwGBitMask = $07E0) and
         (dwBBitMask = $001F) and (dwABitMask = $0000) then
        Result := DXGI_FORMAT_B5G6R5_UNORM
      else if (dwRBitMask = $7C00) and (dwGBitMask = $03E0) and
              (dwBBitMask = $001F) and (dwABitMask = $8000) then
        Result := DXGI_FORMAT_B5G5R5A1_UNORM
      else
        Result := DXGI_FORMAT_R8G8_UNORM

    else if dwRGBBitCount = 8 then
      if dwFlags and DDPF_ALPHA <> 0 then
        Result := DXGI_FORMAT_A8_UNORM
      else
        Result := DXGI_FORMAT_R8_UNORM

    else
      raise Exception.Create('Unsupported uncompressed DDS format');
  end;
end;

procedure SetDDSHeader(aDDSData: Pointer; aDXGI: TDXGI;
  aWidth, aHeight, aMipMapCount: Integer; aCubeMap: Boolean);
var
  DDSHeader: PDDSHeader;
  DDSHeaderDX10: PDDSHeaderDX10;
begin
  DDSHeader := aDDSData;
  DDSHeaderDX10 := Pointer(PByte(aDDSData) + SizeOf(TDDSHeader));

  // header
  with DDSHeader^ do begin
    Magic := MAGIC_DDS;
    dwSize := SizeOf(TDDSHeader) - SizeOf(TMagic4);
    ddspf.dwSize := SizeOf(ddspf);
    dwWidth := aWidth;
    dwHeight := aHeight;
    dwFlags := DDSD_CAPS or DDSD_PIXELFORMAT or DDSD_WIDTH or DDSD_HEIGHT or DDSD_MIPMAPCOUNT;
    dwCaps := DDSCAPS_TEXTURE;
    dwDepth := 1;
    dwMipMapCount := aMipMapCount;
    if dwMipMapCount = 0 then Inc(dwMipMapCount);
    if dwMipMapCount > 1 then
      dwCaps := dwCaps or DDSCAPS_MIPMAP or DDSCAPS_COMPLEX;

    if aCubeMap then begin
      // Archive2.exe creates invalid textures like this
      // dwCaps := dwCaps or DDSCAPS2_CUBEMAP or DDSCAPS_COMPLEX or DDSCAPS2_CUBEMAP_ALLFACES
      // this is the correct way
      dwCaps := dwCaps or DDSCAPS_COMPLEX;
      dwCaps2 := DDSCAPS2_CUBEMAP or DDSCAPS2_CUBEMAP_ALLFACES;
    end;
  end;

  // DXGI specific settings
  with DDSHeader^ do
  case aDXGI of
    DXGI_FORMAT_BC1_UNORM: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DXT1;
      dwPitchOrLinearSize := dwWidth * dwHeight div 2;
    end;
    DXGI_FORMAT_BC2_UNORM: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DXT3;
      dwPitchOrLinearSize := dwWidth * dwHeight;
    end;
    DXGI_FORMAT_BC3_UNORM: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DXT5;
      dwPitchOrLinearSize := dwWidth * dwHeight;
    end;
    DXGI_FORMAT_BC4_SNORM: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_BC4S;
      dwPitchOrLinearSize := dwWidth * dwHeight div 2;
    end;
    DXGI_FORMAT_BC4_UNORM: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_BC4U;
      dwPitchOrLinearSize := dwWidth * dwHeight div 2;
    end;
    DXGI_FORMAT_BC5_SNORM: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_BC5S;
      dwPitchOrLinearSize := dwWidth * dwHeight;
    end;
    DXGI_FORMAT_BC5_UNORM: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_BC5U;
      dwPitchOrLinearSize := dwWidth * dwHeight;
    end;
    DXGI_FORMAT_BC1_UNORM_SRGB: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DX10;
      dwPitchOrLinearSize := dwWidth * dwHeight div 2;
    end;
    DXGI_FORMAT_BC2_UNORM_SRGB, DXGI_FORMAT_BC3_UNORM_SRGB,
    DXGI_FORMAT_BC6H_UF16, DXGI_FORMAT_BC6H_SF16,
    DXGI_FORMAT_BC7_UNORM, DXGI_FORMAT_BC7_UNORM_SRGB: begin
      dwFlags := dwFlags or DDSD_LINEARSIZE;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DX10;
      dwPitchOrLinearSize := dwWidth * dwHeight;
    end;
    DXGI_FORMAT_B8G8R8A8_UNORM_SRGB, DXGI_FORMAT_B8G8R8X8_UNORM_SRGB,
    DXGI_FORMAT_R8G8B8A8_SINT, DXGI_FORMAT_R8G8B8A8_UINT, DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DX10;
      dwPitchOrLinearSize := dwWidth * 4;
    end;
    DXGI_FORMAT_R8G8_SINT, DXGI_FORMAT_R8G8_UINT: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DX10;
      dwPitchOrLinearSize := dwWidth * 2;
    end;
    DXGI_FORMAT_R8_SINT, DXGI_FORMAT_R8_SNORM, DXGI_FORMAT_R8_UINT: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_FOURCC;
      ddspf.dwFourCC := MAGIC_DX10;
      dwPitchOrLinearSize := dwWidth;
    end;
    DXGI_FORMAT_R8G8B8A8_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_RGB or DDPF_ALPHAPIXELS;
      ddspf.dwRGBBitCount := 32;
      ddspf.dwRBitMask := $000000FF;
      ddspf.dwGBitMask := $0000FF00;
      ddspf.dwBBitMask := $00FF0000;
      ddspf.dwABitMask := $FF000000;
      dwPitchOrLinearSize := dwWidth * 4;
    end;
    DXGI_FORMAT_B8G8R8A8_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_RGB or DDPF_ALPHAPIXELS;
      ddspf.dwRGBBitCount := 32;
      ddspf.dwRBitMask := $00FF0000;
      ddspf.dwGBitMask := $0000FF00;
      ddspf.dwBBitMask := $000000FF;
      ddspf.dwABitMask := $FF000000;
      dwPitchOrLinearSize := dwWidth * 4;
    end;
    DXGI_FORMAT_B8G8R8X8_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_RGB;
      ddspf.dwRGBBitCount := 32;
      ddspf.dwRBitMask := $00FF0000;
      ddspf.dwGBitMask := $0000FF00;
      ddspf.dwBBitMask := $000000FF;
      dwPitchOrLinearSize := dwWidth * 4;
    end;
    DXGI_FORMAT_B5G6R5_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_RGB;
      ddspf.dwRGBBitCount := 16;
      ddspf.dwRBitMask := $0000F800;
      ddspf.dwGBitMask := $000007E0;
      ddspf.dwBBitMask := $0000001F;
      dwPitchOrLinearSize := dwWidth * 2;
    end;
    DXGI_FORMAT_B5G5R5A1_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_RGB or DDPF_ALPHAPIXELS;
      ddspf.dwRGBBitCount := 16;
      ddspf.dwRBitMask := $00007C00;
      ddspf.dwGBitMask := $000003E0;
      ddspf.dwBBitMask := $0000001F;
      ddspf.dwABitMask := $00008000;
      dwPitchOrLinearSize := dwWidth * 2;
    end;
    DXGI_FORMAT_R8G8_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_LUMINANCE OR DDPF_ALPHAPIXELS;
      ddspf.dwRGBBitCount := 16;
      ddspf.dwRBitMask := $000000FF;
      ddspf.dwABitMask := $0000FF00;
      dwPitchOrLinearSize := dwWidth * 2;
    end;
    DXGI_FORMAT_A8_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_ALPHA;
      ddspf.dwRGBBitCount := 8;
      ddspf.dwABitMask := $000000FF;
      dwPitchOrLinearSize := dwWidth;
    end;
    DXGI_FORMAT_R8_UNORM: begin
      dwFlags := dwFlags or DDSD_PITCH;
      ddspf.dwFlags := DDPF_LUMINANCE;
      ddspf.dwRGBBitCount := 8;
      ddspf.dwRBitMask := $000000FF;
      dwPitchOrLinearSize := dwWidth;
    end;
  end;

  // additional DX10 header
  if DDSHeader.ddspf.dwFourCC = MAGIC_DX10 then begin
    DDSHeaderDX10.dxgiFormat := Integer(aDXGI);
    DDSHeaderDX10.resourceDimension := DDS_DIMENSION_TEXTURE2D;
    DDSHeaderDX10.arraySize := 1;
    if DDSHeader.dwCaps2 and DDSCAPS2_CUBEMAP <> 0 then
      DDSHeaderDX10.miscFlags := DDS_RESOURCE_MISC_TEXTURECUBE;
  end;
end;

function GetBitsPerPixel(aDXGI: TDXGI): Byte;
begin
  case aDXGI of
    DXGI_FORMAT_BC1_UNORM, DXGI_FORMAT_BC1_UNORM_SRGB,
    DXGI_FORMAT_BC4_UNORM, DXGI_FORMAT_BC4_SNORM:
      Result := 4;
    DXGI_FORMAT_BC2_UNORM, DXGI_FORMAT_BC2_UNORM_SRGB,
    DXGI_FORMAT_BC3_UNORM, DXGI_FORMAT_BC3_UNORM_SRGB,
    DXGI_FORMAT_BC5_UNORM, DXGI_FORMAT_BC5_SNORM,
    DXGI_FORMAT_BC6H_SF16, DXGI_FORMAT_BC6H_UF16,
    DXGI_FORMAT_BC7_UNORM, DXGI_FORMAT_BC7_UNORM_SRGB,
    DXGI_FORMAT_A8_UNORM,
    DXGI_FORMAT_R8_SINT, DXGI_FORMAT_R8_SNORM,
    DXGI_FORMAT_R8_UINT, DXGI_FORMAT_R8_UNORM:
      Result := 8;
    DXGI_FORMAT_B5G6R5_UNORM, DXGI_FORMAT_B5G5R5A1_UNORM,
    DXGI_FORMAT_R8G8_SINT, DXGI_FORMAT_R8G8_UINT, DXGI_FORMAT_R8G8_UNORM:
      Result := 16;
    DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,
    DXGI_FORMAT_B8G8R8X8_UNORM, DXGI_FORMAT_B8G8R8X8_UNORM_SRGB,
    DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_FORMAT_R8G8B8A8_SINT,
    DXGI_FORMAT_R8G8B8A8_UINT, DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
      Result := 32;
    else
      raise Exception.Create('Unsupported DDS format');
  end;
end;

function IsDDSCubeMap(aDDSData: Pointer): Boolean;
begin
  Result := PDDSHeader(aDDSData).dwCaps2 and DDSCAPS2_CUBEMAP <> 0;
end;


end.
