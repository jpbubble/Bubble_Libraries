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

        #region Constructor
        public BubbleBank(BubbleState fromparent) {
            Parent = fromparent;
            state.DoString(@"
                function EndianName(i)
                    if     i==0 then return 'CPU based'
                    elseif i==1 then return 'LittleEndian'
                    elseif i==2 then return 'BigEndian'
                    else   return 'Unknown' end
                end

                function IsLittleEndian() return Bubble_Bank.Endian==1 end
                function isBigEndian() return Bubble_Bank.Endian==2 end
            ", "Bank init chunk");
        }
        #endregion

    }

}
