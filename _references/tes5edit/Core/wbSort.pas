{******************************************************************************

  This Source Code Form is subject to the terms of the Mozilla Public License,
  v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
  one at https://mozilla.org/MPL/2.0/.

*******************************************************************************}

unit wbSort;

{$I wbDefines.inc}

interface

uses
  Classes, wbInterface;

type
  TListSortComparePtr = TListSortCompare;
  TListSortCompare32 = function(Item1, Item2: Cardinal): Integer;
  TListSortCompare64 = function(const Item1, Item2: UInt64): Integer;

procedure wbMergeSortPtr(aList: Pointer; aCount: Integer; aCompare: TListSortComparePtr);
procedure wbMergeSort32(aList: Pointer; aCount: Integer; aCompare: TListSortCompare32);
procedure wbMergeSort64(aList: Pointer; aCount: Integer; aCompare: TListSortCompare64);

type
  TwbMergeSort<T> = class
  public
  type
    TPtr = ^T;
    TArray = array[0..0] of T;
    PArray = ^TArray;
    TListSortCompareTPtr = function(Item1, Item2: TPtr): Integer;
  private
    class procedure InsertionSort(aList: PArray; left, right: integer; aCompare: TListSortCompareTPtr); static;
    class procedure MergeSort(ptrList: PArray; left, right: Integer; aCompare: TListSortCompareTPtr; Buffer: PArray); static;

    class procedure UseStackBufferLarge(aList: Pointer; aCount: Integer; aCompare: TListSortCompareTPtr); static;
    class procedure UseStackBufferSmall(aList: Pointer; aCount: Integer; aCompare: TListSortCompareTPtr); static;
  public
    class procedure Sort(aList: Pointer; aCount: Integer; aCompare: TListSortCompareTPtr); static;
  end;

implementation

type
  TwbMove = procedure(const Source; var Dest; Count : Integer);

var
  wbMove    : TwbMove = nil;

const
  // x64 - use InsertionSort instead MergeSort for small arrays
  MIN_SIZE = 32;

procedure InsertionSortPtr(aList: PwbPointerArray; left, right: integer; aCompare: TListSortComparePtr);
var
  i: Integer;
  j: integer;
  temp: Pointer;
begin
  for i := Succ(left) to right do begin
    j := i;
    temp := aList[j];
    while (j > left) AND (aCompare(temp, aList[Pred(j)]) < 0) do begin
      aList[j] := aList[Pred(j)];
      dec(j);
    end;
    aList[j] := temp;
 end;
end;

procedure MergeSortPtr(ptrList: PwbPointerArray; left: Integer; right: Integer; aCompare: TListSortComparePtr; Buffer: PwbPointerArray);
var
  i, j, k, mid, aCount: Integer;
begin
  mid := (left + right) div 2;
  if (left < mid) then begin
    if (mid - left) <= MIN_SIZE then begin
      InsertionSortPtr(ptrList, left, mid, aCompare)
    end
    else begin
      MergeSortPtr(ptrList, left, mid, aCompare, Buffer);
    end;
  end;
  if (succ(mid) < right) then begin
    if (right - succ(mid)) <= MIN_SIZE then begin
      InsertionSortPtr(ptrList, succ(mid), right, aCompare);
    end
    else begin
       MergeSortPtr(ptrList, succ(mid), right, aCompare, Buffer);
    end;
  end;
  if aCompare(ptrList[mid], ptrList[Succ(mid)]) < 0 then
    exit;
  aCount := succ(mid - left);
  Move(ptrList[left], Buffer[0], aCount * SizeOf(Pointer));
  i := 0;
  j := succ(mid);
  k := left;
  while (i < aCount) and (j <= right) do begin
    if (aCompare(Buffer[i], ptrList[j]) <= 0) then begin
      ptrList[k] := Buffer[i];
      inc(i);
    end else begin
      ptrList[k] := ptrList[j];
      inc(j);
    end;
    inc(k);
  end;
  if (i < aCount) then begin
    Move(Buffer[i], ptrList[k], (aCount - i) * SizeOf(Pointer));
  end;
end;

procedure wbMergeSortPtr(aList: Pointer; aCount: Integer; aCompare: TListSortComparePtr);
var
  Buffer: Pointer;
begin
  if (aCount < 2) or (not Assigned(aList)) then
    Exit;
  if aCount <= MIN_SIZE then begin
    InsertionSortPtr(aList, 0, Pred(aCount), aCompare);
  end
  else begin
    GetMem(Buffer, aCount * SizeOf(Pointer));
    MergeSortPtr(aList, 0, Pred(aCount), aCompare, Buffer);
    FreeMem(Buffer, aCount * SizeOf(Pointer));
  end;
end;

procedure InsertionSort32(aList: PwbCardinalArray; left, right: integer; aCompare: TListSortCompare32);
var
  i: Integer;
  j: integer;
  temp: Cardinal;
begin
  for i := Succ(left) to right do begin
    j := i;
    temp := aList[j];
    while (j > left) AND (aCompare(temp, aList[Pred(j)]) < 0) do begin
      aList[j] := aList[Pred(j)];
      dec(j);
    end;
    aList[j] := temp;
 end;
end;

procedure MergeSort32(ptrList: PwbCardinalArray; left: Integer; right: Integer; aCompare: TListSortCompare32; Buffer: PwbCardinalArray);
var
  i, j, k, mid, aCount: Integer;
begin
  mid := (left + right) div 2;
  if (left < mid) then begin
    if (mid - left) <= MIN_SIZE then begin
      InsertionSort32(ptrList, left, mid, aCompare)
    end
    else begin
      MergeSort32(ptrList, left, mid, aCompare, Buffer);
    end;
  end;
  if (succ(mid) < right) then begin
    if (right - succ(mid)) <= MIN_SIZE then begin
      InsertionSort32(ptrList, succ(mid), right, aCompare);
    end
    else begin
       MergeSort32(ptrList, succ(mid), right, aCompare, Buffer);
    end;
  end;
  if aCompare(ptrList[mid], ptrList[Succ(mid)]) < 0 then
    exit;
  aCount := succ(mid - left);
  Move(ptrList[left], Buffer[0], aCount * SizeOf(Cardinal));
  i := 0;
  j := succ(mid);
  k := left;
  while (i < aCount) and (j <= right) do begin
    if (aCompare(Buffer[i], ptrList[j]) <= 0) then begin
      ptrList[k] := Buffer[i];
      inc(i);
    end else begin
      ptrList[k] := ptrList[j];
      inc(j);
    end;
    inc(k);
  end;
  if (i < aCount) then begin
    Move(Buffer[i], ptrList[k], (aCount - i) * SizeOf(Cardinal));
  end;
end;

procedure wbMergeSort32(aList: Pointer; aCount: Integer; aCompare: TListSortCompare32);
var
  Buffer: Pointer;
begin
  if (aCount < 2) or (not Assigned(aList)) then
    Exit;
  if aCount <= MIN_SIZE then begin
    InsertionSort32(aList, 0, Pred(aCount), aCompare);
  end
  else begin
    GetMem(Buffer, aCount * SizeOf(Cardinal));
    MergeSort32(aList, 0, Pred(aCount), aCompare, Buffer);
    FreeMem(Buffer, aCount * SizeOf(Cardinal));
  end;
end;

procedure wbMergeSort64(aList: Pointer; aCount: Integer; aCompare: TListSortCompare64);
begin
  wbMergeSortPtr(aList, aCount, TListSortComparePtr(aCompare));
end;

{$R-} //range checking must be off

class procedure TwbMergeSort<T>.InsertionSort(aList: PArray; left, right: integer; aCompare: TListSortCompareTPtr);
var
  i: Integer;
  j: integer;
  temp: T;
begin
  for i := Succ(left) to right do begin
    j := i;
    temp := aList[j];
    while (j > left) AND (aCompare(@temp, @aList[Pred(j)]) < 0) do begin
      aList[j] := aList[Pred(j)];
      dec(j);
    end;
    aList[j] := temp;
 end;
end;

class procedure TwbMergeSort<T>.MergeSort(ptrList: PArray; left, right: Integer; aCompare: TListSortCompareTPtr; Buffer: PArray);
var
  i, j, k, mid, aCount: Integer;
begin
  mid := (left + right) div 2;
  if (left < mid) then begin
    if (mid - left) <= MIN_SIZE then begin
      InsertionSort(ptrList, left, mid, aCompare)
    end
    else begin
      MergeSort(ptrList, left, mid, aCompare, Buffer);
    end;
  end;
  if (succ(mid) < right) then begin
    if (right - succ(mid)) <= MIN_SIZE then begin
      InsertionSort(ptrList, succ(mid), right, aCompare);
    end
    else begin
       MergeSort(ptrList, succ(mid), right, aCompare, Buffer);
    end;
  end;
  if aCompare(@ptrList[mid], @ptrList[Succ(mid)]) < 0 then
    exit;
  aCount := succ(mid - left);
  Move(ptrList[left], Buffer[0], aCount * SizeOf(T));
  i := 0;
  j := succ(mid);
  k := left;
  while (i < aCount) and (j <= right) do begin
    if (aCompare(@Buffer[i], @ptrList[j]) <= 0) then begin
      ptrList[k] := Buffer[i];
      inc(i);
    end else begin
      ptrList[k] := ptrList[j];
      inc(j);
    end;
    inc(k);
  end;
  if (i < aCount) then begin
    Move(Buffer[i], ptrList[k], (aCount - i) * SizeOf(T));
  end;
end;

class procedure TwbMergeSort<T>.UseStackBufferLarge(aList: Pointer; aCount: Integer; aCompare: TListSortCompareTPtr);
var
  Buffer: array[0..Pred(4 * 512)] of T;
begin
  MergeSort(aList, 0, Pred(aCount), aCompare, @Buffer);
end;

class procedure TwbMergeSort<T>.UseStackBufferSmall(aList: Pointer; aCount: Integer; aCompare: TListSortCompareTPtr);
var
  Buffer: array[0..Pred(512)] of T;
begin
  MergeSort(aList, 0, Pred(aCount), aCompare, @Buffer);
end;

class procedure TwbMergeSort<T>.Sort(aList: Pointer; aCount: Integer; aCompare: TListSortCompareTPtr);

var
  Buffer: Pointer;
begin
  if (aCount < 2) or (not Assigned(aList)) then
    Exit;

  if aCount <= MIN_SIZE then
    InsertionSort(aList, 0, Pred(aCount), aCompare)
  else if aCount > 4 * 512 then begin
    GetMem(Buffer, aCount * SizeOf(T));
    MergeSort(aList, 0, Pred(aCount), aCompare, Buffer);
    FreeMem(Buffer);
  end else if aCount > 512 then
    UseStackBufferLarge(aList, aCount, aCompare)
  else
    UseStackBufferSmall(aList, aCount, aCompare);
end;

initialization
  wbMove := @Move;
finalization
end.

