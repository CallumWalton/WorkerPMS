using System;
using System.Collections.Generic;
using System.Text;

namespace WorkerPMS
{   
    
    public enum ServerPackets
    {
        Welcome = 1,
        Auth_MSG,
        Reg_MSG
    }
    public enum ClientPackets
    {
        WelcomeRecieved = 1,
        Auth_Req,
        Reg_Req
    }
    public class Packet : IDisposable
    {
        private List<byte> buffer;
        private byte[] readBuffer;
        private int readPosition;

        public Packet()
        {
            buffer = new List<byte>();
            readPosition = 0;
        }
        public Packet(int _id)
        {
            buffer = new List<byte>();
            readPosition = 0;

            Write(_id);
        }
        public Packet(byte[] _data)
        {
            buffer = new List<byte>();
            readPosition = 0;
            SetBytes(_data);
        }
        public void SetBytes(byte[] _data)
        {
            Write(_data);
            readBuffer = buffer.ToArray();
        }
        public void WriteLength()
        {
            buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));
        }

        public void InsertInt(int _int)
        {
            buffer.InsertRange(0, BitConverter.GetBytes(_int));
        }
        public byte[] ToArray()
        {
            readBuffer = buffer.ToArray();
            return readBuffer;
        }
        public int Length()
        {
            return buffer.Count;
        }
        public int UnreadLength()
        {
            return Length() - readPosition;
        }
        public void Reset(bool reset = true)
        {
            if (reset)
            {
                buffer.Clear();
                readBuffer = null;
                readPosition = 0;
            }
            else
            {
                readPosition -= 4;
            }
        }

        public void Write(byte _value)
        {
            buffer.Add(_value);
        }
        public void Write(byte[] _value)
        {
            buffer.AddRange(_value);
        }
        public void Write(short _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        public void Write(int _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        public void Write(long _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        public void Write(float _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        public void Write(bool _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        public void Write(string _value)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes(_value));
        }

        public byte ReadByte(int length, bool readPosUpdate = true)
        {
            if (buffer.Count > readPosition)
            {
                byte value = readBuffer[readPosition];
                if (readPosUpdate)
                {
                    readPosition++;
                }
                return value;
            }
            else
            {
                throw new Exception("Value unreadable");
            }
        }
        public byte[] ReadBytes(int length, bool readPosUpdate = true)
        {
            if (buffer.Count > readPosition)
            {
                byte[] value = buffer.GetRange(readPosition, length).ToArray();
                if (readPosUpdate)
                {
                    readPosition += length;
                }
                return value;
            }
            else
            {
                throw new Exception("Value unreadable");
            }
        }
        public short ReadShort(bool readPosUpdate = true)
        {
            if (buffer.Count > readPosition)
            {
                short value = BitConverter.ToInt16(readBuffer, readPosition);
                if (readPosUpdate)
                {
                    readPosition += 2;
                }
                return value;
            }
            else
            {
                throw new Exception("Value unreadable");
            }
        }
        public long ReadLong(bool readPosUpdate = true)
        {
            if (buffer.Count > readPosition)
            {
                long value = BitConverter.ToInt64(readBuffer, readPosition);
                if (readPosUpdate)
                {
                    readPosition += 2;
                }
                return value;
            }
            else
            {
                throw new Exception("Value unreadable");
            }
        }
        public int ReadInt(bool readPosUpdate = true)
        {
            if (buffer.Count > readPosition)
            {
                int value = BitConverter.ToInt32(readBuffer, readPosition);
                if (readPosUpdate)
                {
                    readPosition += 4;
                }
                return value;
            }
            else
            {
                throw new Exception("Value unreadable");
            }
        }
        public float ReadFloat(bool readPosUpdate = true)
        {
            if (buffer.Count > readPosition)
            {
                float value = BitConverter.ToSingle(readBuffer, readPosition);
                if (readPosUpdate)
                {
                    readPosition += 4;
                }
                return value;
            }
            else
            {
                throw new Exception("Value Unreadable");
            }
        }
        public bool ReadBool(bool readPosUpdate = true)
        {

            if (buffer.Count > readPosition)
            {
                bool value = BitConverter.ToBoolean(readBuffer, readPosition);
                if (readPosUpdate)
                {
                    readPosition += 1;
                }
                return value;
            }
            else
            {
                throw new Exception("Value Unreadable");
            }
        }
        public string ReadString(bool readPosUpdate = true)
        {
            try
            {
                int length = ReadInt();
                string value = Encoding.ASCII.GetString(readBuffer, readPosition, length);
                if (readPosUpdate)
                {
                    readPosition += length;
                }
                return value;
            }
            catch
            {
                throw new Exception("Value Unreadable");
            }
        }

        private bool disposed = false;
        protected virtual void Dispose(bool _disposing)
        {
            if (!disposed)
            {
                if (_disposing)
                {
                    buffer = null;
                    readBuffer = null;
                    readPosition = 0;
                }
                disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
