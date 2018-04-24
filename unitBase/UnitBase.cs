using System;
using System.IO;
using superService;

namespace Connection
{
    public class UnitBase : SuperService
    {
        private Action<bool, MemoryStream> sendDataCallBack;

        private Action<bool, byte[]> sendDataCallBack2;

        private Action closeCallBack;

        internal void Init(Action<bool, MemoryStream> _sendDataCallBack, Action<bool, byte[]> _sendDataCallBack2, Action _closeCallBack)
        {
            sendDataCallBack = _sendDataCallBack;

            sendDataCallBack2 = _sendDataCallBack2;

            closeCallBack = _closeCallBack;
        }

        public virtual void Init()
        {

        }

        public virtual void Kick()
        {
            sendDataCallBack = null;

            sendDataCallBack2 = null;

            if (closeCallBack != null)
            {
                closeCallBack();

                closeCallBack = null;
            }
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

        public void SendData(bool _isPush, byte[] _bytes)
        {
            if (sendDataCallBack2 != null)
            {
                sendDataCallBack2(_isPush, _bytes);
            }
        }
    }
}