using System;
using System.IO;
using superService;

namespace Connection
{
    public class UnitBase : SuperService
    {
        private Action<bool, MemoryStream> sendDataCallBack;

        internal void Init(Action<bool, MemoryStream> _sendDataCallBack)
        {
            sendDataCallBack = _sendDataCallBack;
        }

        public virtual void Init()
        {

        }

        public virtual void Kick()
        {
            sendDataCallBack = null;
        }

        public virtual void ReceiveData(byte[] _bytes)
        {
            throw new NotImplementedException();
        }

        public void SendData(bool _isPush, MemoryStream _ms)
        {
            if (sendDataCallBack != null)
            {
                sendDataCallBack(_isPush, _ms);
            }
        }
    }
}