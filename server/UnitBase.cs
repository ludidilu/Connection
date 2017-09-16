using System;
using System.IO;

namespace Connection
{
    public class UnitBase
    {
        private Action<bool, MemoryStream> sendDataCallBack;

        internal void Init(Action<bool, MemoryStream> _sendDataCallBack)
        {
            sendDataCallBack = _sendDataCallBack;
        }

        public virtual byte[] Login(long _tick)
        {
            throw new NotImplementedException();
        }

        public virtual void ReceiveData(byte[] _bytes, long _tick)
        {
            throw new NotImplementedException();
        }

        public void SendData(bool _isPush, MemoryStream _ms)
        {
            sendDataCallBack(_isPush, _ms);
        }
    }
}