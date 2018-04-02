﻿using System;
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

        public virtual void Init()
        {

        }

        internal void Kick()
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