using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace figcompiler
{

    class Snapshot
    {
        const uint HeaderSize = 27;
        const uint Origin = 0x4000;
        byte[] Bytes;

        private uint AddressToOffset( UInt16 address )
        {
            return address - Origin + HeaderSize;
        }

        public void Load( string filename )
        {
            Bytes = File.ReadAllBytes(filename);
        }

        public void Save(string filename)
        {
            File.WriteAllBytes(filename, Bytes);
        }

        public UInt16 GetWord( UInt16 address )
        {
            uint offset = AddressToOffset(address);
            return (UInt16)(Bytes[offset] + Bytes[(offset + 1) % 65536] * 256);
        }

        public void SetWord( UInt16 address, UInt16 word  )
        {
            uint offset = AddressToOffset(address);
            Bytes[offset] = (byte)(word & 255);
            Bytes[ (offset+1) % 65536 ] = (byte)((word >> 8 ) & 255);
        }

        public byte GetByte( UInt16 address )
        {
            uint offset = AddressToOffset(address);
            return Bytes[ offset ];
        }

        public void SetByte( UInt16 address, byte b)
        {
            uint offset = AddressToOffset(address);
            Bytes[offset] = b;
        }

        public void SetStack(UInt16 address)
        {
            UInt16 Sp = (UInt16)(Bytes[23] + (Bytes[24] << 8));
            SetWord(Sp, address);
        }
 
    }

    class Forth
    {
        enum CF 
        {
            EXIT        = 0x6449,
            DOCOLON     = 0x6611, 
            PUSHCONST   = 0x6653,
            PUSHPFA     = 0x666D,
            STORE       = 0x65D0,
            LIT         = 0x6158,
            SYSVAR      = 0x667f,
            CSTORE      = 0x65df,
            FETCH       = 0x6599,
            ADD         = 0x64cb,
            CMOVE       = 0x631f,
            NEW         = 0x74DD,
            SPSTORE     = 0x640c,
            SPFETCH     = 0x63fd,
            SWAP        = 0x654B,
            TWODUP      = 0x6566,
            XOR         = 0x63ea,
            LTZERO      = 0x64b9,
            ZBRANCH     = 0x6194,
            SUB         = 0x6849,
            DROP        = 0x653e,
            EQUALZ      = 0x64a5,
            BRANCH      = 0x617C,
            CR          = 0x6312,
            TWOFETCH    = 0x65b6,
            PUSHR       = 0x6474,
            OVER        = 0x652f,
            DUP         = 0x6558,
            I           = 0x620d,
            POPR        = 0x648A,
            UDIV        = 0x6371,
            ROT         = 0x68b3,
            LT          = 0x6863,
            PLUSSTORE   = 0x6574,
            OR          = 0x63d7,
            DO          = 0x61f1,
            CFETCH      = 0x65a8,
            EMIT        = 0x7652,
            LOOP        = 0x61aa,
            EMITC       = 0x75bc,
            MINUS       = 0x64fb,
            KEY         = 0x62f4,
            CLS         = 0x9308,
            DODOES      = 0x6acc,
            RPSTORE     = 0x6431,
            LEAVE       = 0x6460,
            ENCLOSE     = 0x629e,
            FILL        = 0x6c7c,
            FIND        = 0x624f,
            DIGIT       = 0x6221,
            EXECUTE     = 0x616d,
            STOD        = 0x70f5,
            UMUL        = 0x633c,
            DPLUS       = 0x64d8,
            TOGGLE      = 0x658b,
            AND         = 0X63C5,
            DMINUS      = 0x6511,
            QTERMINAL   = 0x6305,
            TWOSTORE    = 0x65EC,
            RELOCATE    = 0x87f4,
            BYE         = 0x798d
        };

        public Forth()
        {
            Ip = XT = W = 0;
            TakeInputFromFile = false;
        }

        Dictionary<UInt16, UInt16> CFAMap;
        public Dictionary<string, UInt16> NameMap;

        string ReadString( UInt16 addr, byte length)
        {
            string name = "";
            for (int i = 0; i < length; i++)
            {
                name += (char)( FImage.GetByte( (UInt16)(addr + i)) & 0x7f);
            }

            return name;
        }

        string GetName( UInt16 addr)
        {
            byte length = (byte)(FImage.GetByte( addr) & 31);
            return ReadString( (UInt16)(addr + 1), length);
        }

        UInt16 GetLink( UInt16 addr)
        {
            UInt16 offset = (UInt16)((FImage.GetByte(addr) & 31) + 1);
            return FImage.GetWord( (UInt16)(addr + offset));
        }

        UInt16 GetCFA( UInt16 addr)
        {
            return (UInt16)(( FImage.GetByte(addr) & 31) + 3 + addr);
        }

        UInt16 GetPFA(UInt16 addr)
        {
            return (UInt16)(GetCFA(addr) + 2);
        }

        void InitMap()
        {
            CFAMap = new Dictionary<UInt16, UInt16>();
            NameMap = new Dictionary<string, UInt16>();
            UInt16 latest =0X9E4F;
            List<string> forth = new List<string>();
            do
            {
                UInt16 cfa = GetCFA( latest);
                CFAMap[ (UInt16)(cfa + 2) ] = latest;
                NameMap[GetName(latest)] = (UInt16)(cfa );
                latest = GetLink( latest);
            } while (latest != 0);
        }

        public void SetImage( Snapshot image, UInt16 entry )
        {
            FImage = image;
            Ip = entry;
            Sp = GetCP();
            InitMap();
        }

        const UInt16 CSP = 0x6112;
        const UInt16 RP = 0x6128;
        const UInt16 UP = 0x6126;

        private UInt16      GetRP()
        {   
            // get the address of the top of the return stack
            return FImage.GetWord(RP);
        }

        private void SetRP( UInt16 value )
        {
            FImage.SetWord(RP, value);
        }

        public void PushR(UInt16 word)
        {
            UInt16 rp = GetRP();
            rp -= 2;
            SetRP(rp);
            FImage.SetWord(rp, word);
        }

        public UInt16 PopR()
        {
            UInt16 rp = GetRP();
            UInt16 word = FImage.GetWord(rp);
            rp += 2;
            SetRP(rp);
            return word;
        }

        private UInt16 GetCP()
        {
            return FImage.GetWord(CSP); ;
        }

        private void SetCP( UInt16 value )
        {
            FImage.SetWord(CSP, value); ;
        }

        bool IsWhitespace( char c )
        {
            return c == '\r' || c == '\n' || c == '\t';
        }

        string GetToken( char delim)
        {
            while( Offset < Source.Length &&  
                  ( Source[ Offset ] == delim || IsWhitespace( Source[ Offset ] )))
            {
                Offset++;
            }

            string token = "";

            if ( Offset == Source.Length )
            {
                TakeInputFromFile = false;
            }

            while( Offset < Source.Length && 
                  Source[ Offset ] != delim && 
                  !IsWhitespace( Source[ Offset ] ))
            {
                    token += Source[Offset];
                    Offset++;
            }

            if ( Offset == Source.Length )
            {
                TakeInputFromFile = false;
            }
            else
            {
                Offset++;
            }

            return token;
        }

        private void Interpret()
        {
            PushR(Ip);
            Ip = (UInt16)(W + 2);

            if ( W == 0x6ce5 )
            {
                if ( TakeInputFromFile )
                {
                    UInt16 delim = Pop();
                    UInt16 here = FImage.GetWord( 0xbbb2 );
                    string token = GetToken( ( char) delim);
                    FImage.SetByte(here, ( byte)(token.Length));
                    for (int i = 0; i < token.Length; i++  )
                    {
                        byte c = (byte)(token[i] & 0xff);
                        FImage.SetByte( (UInt16)(here + i + 1), c);
                    }

                    FImage.SetByte( (UInt16)(here + token.Length + 1), 32 );
                    FImage.SetByte( (UInt16)(here + token.Length + 2), 32);

                    Next();
                    Exit();
                }
            }
        }

        private void Exit()
        {
            Ip = PopR();
        }

        private void Next()
        {
            if ( (CF)XT == CF.EXECUTE )
            {
                XT = FImage.GetWord(W);
            }
            else
            { 
                W =  FImage.GetWord(Ip);
                XT = FImage.GetWord(W);
                Ip += 2;
            }
        }

        public void Push( UInt16 word )
        {
            Sp -= 2;
            FImage.SetWord(Sp, word);
        }

        public UInt16 Pop()
        {
            UInt16 word = FImage.GetWord(Sp);
            Sp += 2;
            return word;
        }


        private void PushConstant()
        {
            var word = FImage.GetWord( (UInt16)( W + 2 ));
            Push(word);
        }

        private void PushPFA()
        {
            Push( (UInt16)(W+2));
        }

        private void Store()
        {
            var hl = Pop();
            var de = Pop();
            FImage.SetWord(hl, de);
        }

        private void Lit()
        {
            var hl = FImage.GetWord(Ip);
            Ip += 2;
            Push(hl);
        }

        private void SysVar()
        {
            var index = FImage.GetByte( (UInt16)(W + 2));
            var up = FImage.GetWord(UP);
            Push( (UInt16)(index + up));
        }

        private void CStore()
        {
            var hl = Pop();
            var de = Pop();
            FImage.SetByte( hl, (byte)(de & 0xff ));
        }

        private void Fetch()
        {
            var hl = Pop();
            var de = FImage.GetWord(hl);
            Push(de);
        }

        public void Add()
        {
            Push((UInt16)(Pop() + Pop()));
        }

        public void CMove()
        {
            var bc = Pop();
            var de = Pop();
            var hl = Pop();
            if ( bc != 0 )
            {
                do
                {
                    FImage.SetByte(de, FImage.GetByte(hl));
                    hl++;
                    de++;
                    bc--;
                } while (bc != 0);
            }
        }

        void SPStore()
        {
            var hl = FImage.GetWord( UP );
            Sp     = FImage.GetWord( (UInt16)(hl + 6));
        }

        void SPFetch()
        {
            Push(Sp);
        }

        void Swap()
        {
            var hl = Pop();
            var de = Pop();
            Push( hl );
            Push(de);
        }

        void TwoDup()
        {
            var hl = Pop();
            var de = Pop();
            Push( de );
            Push(hl);
            Push(de);
            Push(hl);
        }

        void LTZero()
        {
            Int16 hl = (Int16)Pop();
            if ( hl < 0)
            {
                Push(1);
            }
            else
            {
                Push(0);
            }
        }

        void Branch()
        {
            UInt16 jmp = FImage.GetWord(Ip);
            Ip += jmp;
        }

        void ZBranch()
        {
            Int16 hl = (Int16)Pop();
            if ( hl != 0 )
            {
                Ip += 2;
            }
            else
            {
                Branch();
            }
        }

        void Sub()
        {
            var de = Pop();
            var hl = Pop();
            Push( (UInt16)(hl - de));
        }

        void Drop()
        {
            Pop();
        }

        void EqualZ()
        {
            if ( Pop() == 0 )
            {
                Push(1);
            }
            else
            {
                Push(0);
            }
        }

        void Cr()
        {
            Console.WriteLine();
        }

        void TwoFetch()
        {
            var hl = Pop();
            var de = FImage.GetWord( (UInt16)(hl +2));
            Push( de );
            de = FImage.GetWord( (UInt16)(hl) );
            Push( de );
        }

        void PushRP()
        {
            PushR(Pop());
        }

        void PopRP()
        {
            Push(PopR());
        }

        void Over()
        {
            var de = Pop();
            var hl = Pop();
            Push(hl);
            Push(de);
            Push(hl);
        }

        void Dup()
        {
            var hl = Pop();
            Push(hl);
            Push(hl);
        }

        void I()
        {
            UInt16 rp = GetRP();
            Push(FImage.GetWord(rp));
        }

        void UDivide()
        {
            Int32 divisor = (Int32)Pop();
            var high = Pop();
            var low  = Pop();

            Int32 d = (Int32)((high << 16) + low);
            if (divisor == 0)
            {
                Push(0xffff);
                Push(0xffff);
            }
            else
            {
                Push((UInt16)(d % divisor));
                Push((UInt16)(d / divisor));
            }
        }

        void UMul()
        {
            Int32 hl = Pop();
            Int32 de = Pop();
            UInt32 product = (UInt32)(hl * de);
            Push( (UInt16)(product & 0xffff));
            Push( (UInt16)((product >> 16) &0xffff ));
        }

        void Rot()
        {
            var hl = Pop();
            var de = Pop();
            var bc = Pop();

            Push(de);
            Push(hl);
            Push(bc);
        }

        void LT()
        {
            var hl = (Int16)Pop();
            var de = (Int16)Pop();
            if ( de < hl )
            {
                Push(1);
            }
            else
            {
                Push(0);
            }
        }

        void PlusStore()
        {
            var hl = Pop();
            var de = Pop();
            UInt16 v = FImage.GetWord(hl);
            FImage.SetWord(hl, (UInt16)(v + de));
        }

        void Xor()
        {
            Push((UInt16)(Pop() ^ Pop()));
        }

        void Or()
        {
            Push( (UInt16)(Pop() | Pop()));
        }

        void And()
        {
            Push((UInt16)(Pop() &  Pop()));
        }

        void Do()
        {
            var hl = Pop();
            var de = Pop();
            PushR(de);
            PushR(hl);
        }

        void Loop( UInt16 step)
        {
            var rp = GetRP();
            var i = FImage.GetWord(rp);
            var limit = FImage.GetWord( (UInt16)(rp + 2));
            i += step;
            if ( i < limit )
            {
                FImage.SetWord(rp, i);
                Branch();
            }
            else
            {
                Ip += 2;
                SetRP( (UInt16)(rp + 4));
            }

        }

        void PlusLoop()
        {
            var de = Pop();
            Loop(de);
        }

        void Loop()
        {
            Loop(1);
        }

        void CFetch()
        {
            var hl = Pop();
            var c = FImage.GetByte(hl);
            Push((UInt16)c);
        }

        void Emit()
        {
            char hl = (char)(Pop() & 0x7f);
            Console.Write(hl);
        }

        void Minus()
        {
            Int16 hl = (Int16)Pop();
            Push( (UInt16)(-hl) );
        }

        void DMinus()
        {
            Int32 d = ( Int32)PopDouble();
            PushDouble((UInt32)(-d));
        }

        void Key()
        {
            if (TakeInputFromFile)
            {
                Push('\r');
            }
            else
            {
                var key = (UInt16)Console.Read();
                if (key != '\n')
                {
                    Push(key);
                }
                else
                {
                    Push(' ');
                }
            }
        }


        void DoDoes()
        {
          PushR( Ip );
          Ip = FImage.GetWord( (UInt16)(W + 2) );
          Push((UInt16)(W + 4));
        }

        void RpStore()
        {
            const UInt16 UP = 0x6126;
            var up = FImage.GetWord(UP);
            var hl = FImage.GetWord((UInt16)(8 + up));
            SetRP(hl);
        }
    
        void Leave()
        {
            var rp = GetRP();
            var index = FImage.GetWord(rp);
            FImage.SetWord((UInt16)(rp + 2), index);
        }

        void Enclose()
        {
            var delim = (byte)( Pop() & 0xff );
            var address = Pop();
            var addr = address;
            int offset = -1;
            byte c;

            do {
                c = FImage.GetByte( address );
                offset++;
                address++;
            } while( c == delim );

            var first = (UInt16)offset;

            do
            {
                c = FImage.GetByte(address);
                offset++;
                address++;
            } while (c != delim && c != 0);

            var nextdelim = (UInt16)offset;
            var nexttoken = nextdelim;
            if (c != 0)
            {
                nexttoken++;
            }

            Push(addr);
            Push(first);
            Push(nextdelim);
            Push(nexttoken);
         }

        void Fill()
        {
            byte item = (byte)Pop();
            var quantity = Pop();
            var address = Pop();
            for( int i = 0; i < quantity; i++)
            {
                FImage.SetByte( (UInt16)(address + i), item );
            }
        }
        
        void Find()
        {
            var latest = Pop();
            var name = Pop();
            var len = FImage.GetByte(name);
            name++;
            string s = "";
            for (int i = 0; i < len; i++)
            {
                s += (char)FImage.GetByte((UInt16)(name + i));
            }

            do
            {
                string nm = GetName(latest);
                if (nm == s)
                {
                    var attr = FImage.GetByte(latest);
                    Push( GetPFA( latest ) );
                    Push((UInt16)attr);
                    Push(1);
                    return;
                }

                latest = GetLink(latest);
            } while (latest != 0);

            Push(0);
        }

        void Digit()
        {
            var b = Pop();
            var c = Pop();
            UInt16 v;

            if ( c >= '0' && c <= '9' )
            {
                v =  (UInt16)(c - '0');
            }
            else if ( c >= 'A' )
            {
                v = (UInt16)(c - 'A' + 10);
            }
            else
            {
                Push(0);
                return;
            }
           
            if ( v <  b )
            {
                Push(v);
                Push(1);
            }
            else
            {
                Push(0);
            }
        }

        void Execute()
        {
            var hl = Pop();
            W = (UInt16)(hl );
        }

        void StoD()
        {
            PushDouble((UInt32)((Int16)Pop()));
        }

        UInt32 PopDouble()
        {
            var high = (UInt32)Pop();
            var low = (UInt32)Pop();

            return (high << 16) + low;
        }

        void PushDouble( UInt32 d)
        {
            var high = (d >> 16) & 0xffff;
            var low = d & 0xffff;
            Push((UInt16)low);
            Push((UInt16)high);
        }

        void DPlus()
        {
            PushDouble(PopDouble() + PopDouble());
        }

        void Toggle()
        {
            var de = Pop();
            var hl = Pop();
            var toggled = (UInt16)(FImage.GetWord(hl) ^ de);
            FImage.SetWord(hl, toggled);    
        }

        void TwoStore()
        {
            var addr = Pop();
            var high = Pop();
            var low = Pop();

            FImage.SetWord(addr, high);
            FImage.SetWord(  (UInt16)(addr + 2), low);
        }

        void Trace( int colons )
        {
            UInt16 nf;
            for (int i = 0; i < colons; i++)
            {
                Console.Write('\t');
            }
            if (CFAMap.TryGetValue(XT, out nf))
            {
                Console.WriteLine("{0}\t{1:X}", GetName(nf), Sp);
            }
            else
            {
                if (XT == 0x6611)
                {
                    if ( CFAMap.TryGetValue( (UInt16)(Ip - 2), out nf) )
                    {
                        Console.WriteLine(":{0}\t{1:X}", GetName( nf ), Sp);
                    }
                    else 
                    {
                        Console.WriteLine("{0:X}\t{1:X}", XT, Sp);
                    }
                }
                else
                {
                    Console.WriteLine("{0:X}\t{1:X}", XT, Sp);
                }
            }
        }

        public void Run( bool breakOnExit )
        {
            int colons = 0;
            do
            {
                Next();
                
               //Trace(colons);
       
                switch ((CF)XT)
                {
                    case CF.DOCOLON:
                        colons++;
                        Interpret();
                        break;
                    case CF.EXIT:
                        Exit();
                        colons--;
                        if ( colons == 0 && breakOnExit )
                        {
                            return;
                        }
                        break;
                    case CF.PUSHCONST:
                        PushConstant();
                        break;
                    case CF.PUSHPFA:
                        PushPFA();
                        break;
                    case CF.STORE:
                        Store();
                        break;
                    case CF.LIT:
                        Lit();
                        break;
                    case CF.SYSVAR:
                        SysVar();
                        break;
                    case CF.CSTORE:
                        CStore();
                        break;
                    case CF.FETCH:
                        Fetch();
                        break;
                    case CF.ADD:
                        Add();
                        break;
                    case CF.CMOVE:
                        CMove();
                        break;
                    case CF.NEW:
                        Push(0x9e5e);
                        break;
                    case CF.SPSTORE:
                        SPStore();
                        break;
                    case CF.SPFETCH:
                        SPFetch();
                        break; 
                    case CF.SWAP:
                        Swap();
                        break;
                    case CF.TWODUP:
                        TwoDup();
                        break;
                    case CF.XOR:
                        Xor();
                        break;
                    case CF.LTZERO:
                        LTZero();
                        break;
                    case CF.ZBRANCH:
                        ZBranch();
                        break;
                    case CF.SUB:
                        Sub();
                        break;
                    case CF.DROP:
                        Drop();
                        break;
                    case CF.EQUALZ:
                        EqualZ();
                        break;
                    case CF.BRANCH:
                        Branch();
                        break;
                    case CF.CR:
                        Cr();
                        break;
                    case CF.TWOFETCH:
                        TwoFetch();
                        break;
                    case CF.PUSHR:
                        PushRP();
                        break;
                    case CF.POPR:
                        PopRP();
                        break;
                    case CF.OVER:
                        Over();
                        break;
                    case CF.DUP:
                        Dup();
                        break;
                    case CF.I:
                        I();
                        break;
                    case CF.UDIV:
                        UDivide();
                        break;
                    case CF.ROT:
                        Rot();
                        break;
                    case CF.LT:
                        LT();
                        break;
                    case CF.PLUSSTORE:
                        PlusStore();
                        break;
                    case CF.OR:
                        Or();
                        break;
                    case CF.AND:
                        And();
                        break;
                    case CF.DO:
                        Do();
                        break;
                    case CF.CFETCH:
                        CFetch();
                        break;
                    case CF.EMIT:
                        Emit();
                        break;
                    case CF.LOOP:
                        Loop();
                        break;
                    case CF.EMITC:
                        Emit();
                        break;
                    case CF.MINUS:
                        Minus();
                        break;
                    case CF.KEY:
                        Key();
                        break;
                    case CF.CLS:
                        break;
                    case CF.DODOES:
                        DoDoes();
                        break;
                    case CF.RPSTORE:
                        RpStore();
                        break;
                    case CF.LEAVE:
                        Leave();
                        break;
                    case CF.ENCLOSE:
                        Enclose();
                        break;
                    case CF.FILL:
                        Fill();
                        break;
                    case CF.FIND:
                        Find();
                        break;
                    case CF.DIGIT:
                        Digit();
                        break;
                    case CF.EXECUTE:
                        Execute();
                        break;
                    case CF.STOD:
                        StoD();
                        break;
                    case CF.UMUL:
                        UMul();
                        break;
                    case CF.DPLUS:
                        DPlus();
                        break;
                    case CF.TOGGLE:
                        Toggle();
                        break;
                    case CF.DMINUS:
                        DMinus();
                        break;
                    case CF.QTERMINAL:
                        Push(0);
                      //  FImage.SetStack( 0x6105 );
                     //   FImage.Save("c:\\wl_save.sna");
                        break;
                    case CF.BYE:
                        FImage.SetStack( 0x6101 );
                        FImage.Save("..\\..\\..\\wl_save.sna");
                        return;
                    case CF.TWOSTORE:
                        TwoStore();
                        break;
                    case CF.RELOCATE:
                        // don't actually need to relocate the sprites on the PC host
                        break;
                    default:

                        UInt16 nf;
                        if (CFAMap.TryGetValue(XT, out nf))
                        {
                            Console.WriteLine("Unknow token {0:X} ({1})", XT, GetName(nf));
                        }
                        else
                        {
                            Console.WriteLine("Unknown Token: {0:X}", XT);
                        }
                      
                        break;
                }
                if ( Sp > 0xbb00 )
                {
                   Console.WriteLine("stack underflow!");
                }
            } while (true);

        }

        public void SetSource( string filename )
        {
            Source = File.ReadAllText(filename);
            Offset = 0;
            TakeInputFromFile = true;
        }

        UInt16             XT;
        public UInt16      Ip;
        UInt16             W;
        public UInt16      Sp;
        Snapshot           FImage;
        string             Source;
        int                Offset;
        bool               TakeInputFromFile;
    }

    class Tests
    {
        public Tests( string snapshot )
        {
            SnapshotFilename = snapshot;
        }

        private void InitFixture()
        {
            Snapshot = new Snapshot();
            Snapshot.Load(SnapshotFilename);
            Forth = new Forth();
            Forth.SetImage(Snapshot, 0x7099 );
        }

        private bool StacksAgree( int[] instack, string word, int[] outstack, bool reset  )
        {
            if (reset)
            {
                InitFixture();
            }
            else
            {
                Forth.Ip = 0x7099;
            }

            foreach( var i in instack )
            {
                Console.Write("{0},", i);
                Forth.Push((UInt16)i);
            }

            Console.Write(" {0} ", word);
            Snapshot.SetWord(0x7099, Forth.NameMap[word] );
            Forth.Run(true);

            foreach( var i in outstack )
            {
                UInt16 sv = Forth.Pop();
                Console.Write("{0},",(Int16)sv );
                if( sv !=  (UInt16)i  )
                {
                    Console.WriteLine(" Failed: Expected {0} - Got {1}", i, sv);
                    return false;
                }
            }

           
            Console.WriteLine(" Passed"); 
            return true;

        }

        public void Run()
        {
      //      StacksAgree(new UInt16[] { 1, 2,3 }, "ROT", new UInt16[] { 1, 3, 2 });
               StacksAgree(new [] { 1, 2 }, "MAX", new [] { 2 }, true);
               StacksAgree(new[] { 1, -2 }, "MAX", new[] { 1 }, true);
               StacksAgree(new[] { 1, 2 }, "MIN", new[] { 1 }, true);
               StacksAgree(new[] { 1, -2 }, "MIN", new[] { -2 }, true);

               StacksAgree(new[] { -2 }, "ABS", new[] { 2 }, true);
               StacksAgree(new[] { 2 }, "ABS", new[] { 2 }, true);

               StacksAgree(new[] { 100,1 }, "#", new[] { 0,6563 }, true);

               StacksAgree(new[] { 100, 1 }, "D.", new int [] {}, true );
               StacksAgree(new int[] {}, ".CPU", new int[] { }, true );
               StacksAgree(new int[] { }, "?STACK", new int[] { }, true);
               StacksAgree( new int[]{1}, "BLOCK", new int[] { 0xbbe2 }, true );
               StacksAgree( new int[]{2}, "BLOCK", new int[] { 0xbc66 }, false );
            
        }

        private string SnapshotFilename;
        Snapshot Snapshot;
        Forth Forth;
    }


    class Program
    {
       
        static void Main(string[] args)
        {
            var forth = new Forth();
            var image = new Snapshot();
            image.Load("..\\..\\..\\base.sna");
            forth.SetImage(image, 0x7099);
            forth.SetSource("..\\..\\..\\test.f");
            forth.Run(false);

           var tests = new Tests("..\\..\\..\\base.sna");
        //    tests.Run();
        }
    }
}
