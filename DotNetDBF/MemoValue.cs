﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DotNetDBF
{
    public class MemoValue
    {
        private bool _loaded;
        private bool _new;

        public string MemoTerminator { get; set; } = "\x1A";

        public int HeaderOffset { get; set; } = 0;

        public MemoValue(string aValue)
        {
            _lockName = $"DotNetDBF.Memo.new.{Guid.NewGuid()}";
            Value = aValue;
        }


        internal MemoValue(long block, DBFBase aBase, 
            string fileLoc, DBFReader.LazyStream fileStream)
        {
            _block = block;
            _base = aBase;
            _fileLoc = fileLoc;
            _fileStream = fileStream;
            if (string.IsNullOrEmpty(fileLoc))
            {
                _lockName = fileStream();
            }
            else
            {
                _lockName = $"DotNetDBF.Memo.read.{_fileLoc}";
            }
        }

        private readonly DBFBase _base;
        private readonly object _lockName;
        private long _block;
        private readonly string _fileLoc;
        private string _value;
        private readonly DBFReader.LazyStream _fileStream;

        internal long Block => _block;

        internal void Write(DBFWriter aBase)
        {
            lock (_lockName)
            {
                if (!_new)
                    return;

                var raf = aBase.DataMemo;

                /* before proceeding check whether the passed in File object
                    is an empty/non-existent file or not.
                    */
                if (raf == null)
                {
                    throw new InvalidDataException("Null Memo Field Stream from Writer");
                }

                var tWriter = new BinaryWriter(raf, aBase.CharEncoding); //Don't close the stream could be used else where;
            
                if (raf.Length == 0)
                {
                    var tHeader = new DBTHeader();
                    tHeader.Write(tWriter);
                    
                }
                
                



                var tValue =  _value;
                if ((tValue.Length + sizeof(int) + 8) % aBase.BlockSize != 0) // 8 because of the header and size below
                {
                    
                    var header = "\u0000\u0000\u0000\u0001";
                    var size = string.Join("", Enumerable.Range(0, 4).Select(_ => "\x00"));
                    tValue = header + size + tValue + MemoTerminator;
                   
                    
                    
                }

                if ((tValue.Length % aBase.BlockSize) != 0)
                {
                    var remainder = tValue.Length % aBase.BlockSize;
                    tValue += string.Join("", Enumerable.Range(0, aBase.BlockSize - remainder).Select(_ => "\x00"));
                }


                
                var tPosition = raf.Seek(0, SeekOrigin.End); //Got To End Of File
                var tBlockDiff = tPosition % aBase.BlockSize;
                if (tBlockDiff != 0)
                {
                    tPosition = raf.Seek(aBase.BlockSize - tBlockDiff, SeekOrigin.Current);
                }
                _block = tPosition / aBase.BlockSize;
                var tData = aBase.CharEncoding.GetBytes(tValue);
                var temp = BitConverter.GetBytes(_value.Length).Reverse().ToArray();
                tData[4] = temp[0];
                tData[5] = temp[1];
                tData[6] = temp[2];
                tData[7] = temp[3];
                



                var tDataLength = tData.Length;
                var tNewDiff = (tDataLength % aBase.BlockSize);
                tWriter.Write(tData);
                if (tNewDiff != 0)
                    tWriter.Seek(aBase.BlockSize - (tDataLength % aBase.BlockSize), SeekOrigin.Current);
                tWriter.Flush();
                    

            }
        }


        public string Value
        {
            get
            {
                lock (_lockName)
                {
                    if (_new || _loaded) return _value;
                    var fileStream = _fileStream();

                    
                    var reader = new BinaryReader(fileStream);
                        
                    {
                        var baseBlockSize = (_block * _base.BlockSize) + HeaderOffset;
                        
                        reader.BaseStream.Seek(baseBlockSize, SeekOrigin.Begin);
                        var builder = new StringBuilder();
                        int termIndex;
                        var softReturn = _base.CharEncoding.GetString(new byte[] {0x8d, 0x0a});

                        do
                        {
                            var blockSize = _base.BlockSize;
                            
                            var data = reader.ReadBytes(blockSize);
                            if ((data.Length == 0))
                            {
                                throw new DBTException("Missing Data for block or no 1a memo terminator");
                            }
                            var stringVal = _base.CharEncoding.GetString(data);
                            termIndex = stringVal.IndexOf(MemoTerminator, StringComparison.Ordinal);
                            


                            if (termIndex != -1)
                                stringVal = stringVal.Substring(0, termIndex);
                            builder.Append(stringVal);
                        } while (termIndex == -1 && reader.BaseStream.Position < reader.BaseStream.Length);
                        _value = builder.ToString().Replace(softReturn, string.Empty);
                    }
                    _loaded = true;

                    return _value;
                }
            }
            set
            {
                lock (_lockName)
                {
                    _new = true;
                    _value = value;
                }
            }
        }

        public override int GetHashCode()
        {
            return _lockName.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }

        public override bool Equals(object obj)
        {
            if (obj is MemoValue m)
            {
                return ReferenceEquals(this, obj) || Value.Equals(m.Value);
            }

            return false;
        }
    }
}