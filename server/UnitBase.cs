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

        public virtual void Kick(bool _logout)
        {
            sendDataCallBack = null;
        }

        public virtual byte[] Login()
        {
            throw new NotImplementedException();
        }

        public virtual void ReceiveData(byte[] _bytes)
        {
            throw new NotImplementedException();
        }

        public void SendData(bool _isPush, MemoryStream _ms)
        {
            sendDataCallBack?.Invoke(_isPush, _ms);
        }
    }
}