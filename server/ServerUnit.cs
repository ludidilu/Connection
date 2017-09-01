using System;
using System.Net.Sockets;
using System.IO;

namespace Connection
{
    internal class ServerUnit<T> where T : UnitBase, new()
    {
        private Socket socket;

        private ushort bodyLength;

        private byte[] uidBuffer = new byte[Const.UID_LENGTH];

        private byte[] headBuffer = new byte[Const.HEAD_LENGTH];

        private byte[] bodyBuffer = new byte[short.MaxValue];

        internal T unit { get; private set; }

        private bool isReceiveHead = true;

        private int lastTick;

        internal void Init(Socket _socket, int _tick)
        {
            socket = _socket;

            lastTick = _tick;
        }

        internal void SetUnit(T _unit)
        {
            unit = _unit;

            unit.Init(SendData);

            unit.Login(SetUnitOver);
        }

        private void SetUnitOver(MemoryStream _ms)
        {
            SendDataReal(Const.PackageTag.LOGIN, _ms);
        }

        internal void Kick()
        {
            if (unit != null)
            {
                unit.Init(null);
            }

            socket.Close();
        }

        internal int CheckLogin(int _tick)
        {
            if (_tick - lastTick > Const.KICK_TICK_LONG)
            {
                Kick();

                return -1;
            }
            else if (socket.Available >= Const.UID_LENGTH)
            {
                socket.Receive(uidBuffer, Const.UID_LENGTH, SocketFlags.None);

                int uid = BitConverter.ToInt32(uidBuffer, 0);

                if (uid < 1)
                {
                    Kick();

                    return -1;
                }
                else
                {
                    return uid;
                }
            }
            else
            {
                return 0;
            }
        }

        internal bool Update(int _tick)
        {
            if (_tick - lastTick > Const.KICK_TICK_LONG)
            {
                Kick();

                return true;
            }

            if (isReceiveHead)
            {
                ReceiveHead(_tick);
            }
            else
            {
                ReceiveBody(_tick);
            }

            return false;
        }

        private void ReceiveHead(int _tick)
        {
            if (socket.Available >= Const.HEAD_LENGTH)
            {
                socket.Receive(headBuffer, Const.HEAD_LENGTH, SocketFlags.None);

                isReceiveHead = false;

                bodyLength = BitConverter.ToUInt16(headBuffer, 0);

                ReceiveBody(_tick);
            }
        }

        private void ReceiveBody(int _tick)
        {
            if (socket.Available >= bodyLength)
            {
                lastTick = _tick;

                socket.Receive(bodyBuffer, bodyLength, SocketFlags.None);

                isReceiveHead = true;

                unit.ReceiveData(bodyBuffer);

                ReceiveHead(_tick);
            }
        }

        internal void SendData(bool _isPush, MemoryStream _ms)
        {
            Const.PackageTag tag = _isPush ? Const.PackageTag.PUSH : Const.PackageTag.REPLY;

            SendDataReal(tag, _ms);
        }

        private void SendDataReal(Const.PackageTag _packageTag, MemoryStream _ms)
        {
            int length = Const.HEAD_LENGTH + Const.TYPE_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)(Const.TYPE_LENGTH + _ms.Length)), bodyBuffer, Const.HEAD_LENGTH);

            bodyBuffer[Const.HEAD_LENGTH] = (byte)_packageTag;

            Array.Copy(_ms.GetBuffer(), 0, bodyBuffer, Const.HEAD_LENGTH + Const.TYPE_LENGTH, _ms.Length);

            socket.BeginSend(bodyBuffer, 0, length, SocketFlags.None, SendCallBack, null);
        }

        private void SendCallBack(IAsyncResult _result)
        {
            socket.EndSend(_result);
        }
    }
}