// Lic:
// BubbleBankClass.cs
// Bank for Bubble
// version: 19.05.09
// Copyright (C)  Jeroen P. Broks
// This software is provided 'as-is', without any express or implied
// warranty.  In no event will the authors be held liable for any damages
// arising from the use of this software.
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
// 1. The origin of this software must not be misrepresented; you must not
// claim that you wrote the original software. If you use this software
// in a product, an acknowledgment in the product documentation would be
// appreciated but is not required.
// 2. Altered source versions must be plainly marked as such, and must not be
// misrepresented as being the original software.
// 3. This notice may not be removed or altered from any source distribution.
// EndLic

/* 
   This library will add banking to Bubble.
   As well as an endian conversion routine in case 
   it's needed
*/

using NLua;
using System;
using System.Collections.Generic;

namespace Bubble {


    internal class TBank {
        byte[] myBuffer;

        public TBank(long size) { myBuffer = new byte[ size ]; }
        public long Length => myBuffer.Length;
        public void Poke(long a, byte v) => myBuffer[a] = v;
        public byte Peek(long a) => myBuffer[a];
    }

    // This routine will be used for the actual API;
    class BubbleBank {
        #region Private crap
        private Dictionary<int, TBank> BankDict = new Dictionary<int, TBank>();
        readonly internal BubbleState Parent;
        Lua state => Parent.state;
        #endregion

        #region Endian detection
        static private byte _endian = 0;
        public byte Endian { get {
                if (_endian != 0) return _endian;
                int checkint = 250;
                var checkbytes = BitConverter.GetBytes(checkint);
                if (checkbytes[0]==250) {
                    _endian = 1;
                    return 1;
                }
                _endian = 2;
                return 2;
            }
        }
        #endregion

        #region Get Bank
        private int FirstFreeBank {
            get {
                int r = 0;
                while (!BankDict.ContainsKey(r)) r++;
                return r;
            }
        }

        private TBank Bank(int i) {
            if (!BankDict.ContainsKey(i)) {
                GetError = $"Bank #{i} not found!";
                return null;
            }
            return BankDict[i];
        }
        #endregion

        #region Constructor
        public BubbleBank(BubbleState fromparent) {
            Parent = fromparent;
            state.DoString(@"
                -- local Bubble_Bank = Bubble_Bank -- Locals are faster than globals, and due to the header functions this may already be a bit slower :-/

                function EndianName(i)
                    if     i==0 then return 'CPU based'
                    elseif i==1 then return 'LittleEndian'
                    elseif i==2 then return 'BigEndian'
                    else   return 'Unknown' end
                end

                function IsLittleEndian() return Bubble_Bank.Endian==1 end
                function IsBigEndian() return Bubble_Bank.Endian==2 end

                function HasBank(i) return Bubble_Bank:HasBank(i) end
                function BankLength(i) return Bubble_Bank:BankLength(i) end
                function CreateBank() return Bubble_Bank:CreateBank(size) end
                function FreeBank(i) return Bubble_Bank:FreeBank() end

                function PokeByte(bank,address,value)         Bubble_Bank:PokeByte (bank,address,value)  end
                function PokeInt32(bank,address,value,endian) Bubble_Bank:PokeInt32(bank,address,value,endian) end
                function PokeInt64(bank,address,value,endian) Bubble_Bank:PokeInt64(bank,address,value,endian) end

                function PeekByte (bank,address,endian) return Bubble_Bank:PeekByte (bank,address)        end
                function PeekInt32(bank,address,endian) return Bubble_Bank:PeekInt32(bank,address,endian) end
                function PeekInt64(bank,address,endian) return Bubble_Bank:PeekInt64(bank,address,endian) end


            ", "Bank init chunk");
        }
        #endregion

        #region API         
        public string GetError { get; private set; } = "";
        public bool HasBank(int i) => BankDict.ContainsKey(i);
        public int CreateBank(long size) {
            GetError = "";
            try {
                var newbank = new TBank(size);
                var ret = FirstFreeBank;
                BankDict[ret] = newbank;
                return ret;
            } catch (Exception e) {
                // The only error I deem likely on this road is "Out of memory", but hey, it's a very valid excuse to have proper catching, no?
                GetError = e.Message;
                return -1;
            }
        }

        public void FreeBank(int i) {
            GetError = "";
            if (!HasBank(i)) { GetError = $"FreeBank({i}): Bank doesn't exist!"; }
            BankDict.Remove(i);
        }

        public long BankLength(int bank) {
            if (!HasBank(bank)) return -1;
            return BankDict[bank].Length;
        }

        public void PokeByte(int bank,long address, byte v) {
            var head = $"Poke({bank},{address},{v}): ";
            if (!HasBank(bank)) throw new Exception($"{head}Bank does not exist!");
            var mybank = BankDict[bank];
            if (address < 0 || address >= mybank.Length) throw new Exception($"{head}Address out of bounds!");
            mybank.Poke(address, v);
        }

        public void PokeInt32(int bank, long address, int v, byte Endian = 1) {
            var vbytes = BitConverter.GetBytes(v);
            if (Endian != 0 && Endian != _endian) Array.Reverse(vbytes);
            for (int i = 0; i < vbytes.Length; i++) PokeByte(bank, address + i, vbytes[i]);
        }

        public void PokeInt64(int bank, long address, long v, byte Endian = 1) {
            var vbytes = BitConverter.GetBytes(v);
            if (Endian != 0 && Endian != _endian) Array.Reverse(vbytes);
            for (int i = 0; i < vbytes.Length; i++) PokeByte(bank, address + i, vbytes[i]);
        }

        public byte PeekByte(int bank,long address) {
            var head = $"Peek({bank},{address}): ";
            if (!HasBank(bank)) throw new Exception($"{head}Bank does not exist!");
            var mybank = BankDict[bank];
            if (address < 0 || address >= mybank.Length) throw new Exception($"{head}Address out of bounds!");
            return mybank.Peek(address);
        }
        
        public int PeekInt32(int bank,long address, byte Endian = 1) {
            byte[] vbytes = new byte[4];
            for (int i = 0; i < vbytes.Length; i++) vbytes[i] = PeekByte(bank, i + address);
            if (Endian != 0 && Endian != _endian) Array.Reverse(vbytes);
            return BitConverter.ToInt32(vbytes, 0);
        }

        public long PeekInt64(int bank, long address, byte Endian = 1) {
            byte[] vbytes = new byte[8];
            for (int i = 0; i < vbytes.Length; i++) vbytes[i] = PeekByte(bank, i + address);
            if (Endian != 0 && Endian != _endian) Array.Reverse(vbytes);
            return BitConverter.ToInt64(vbytes, 0);
        }


        #endregion

    }

}

