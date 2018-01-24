using System;
using System.Net.Sockets;
using System.IO;

namespace Connection
{
    internal class ServerUnit<T> where T : UnitBase, new()
    {
        private Socket socket;

        private ushort bodyLength;

        private byte[] uidBuffer = new byte[Constant.UID_LENGTH];

        private byte[] headBuffer = new byte[Constant.HEAD_LENGTH];

        private byte[] receiveBodyBuffer = new byte[short.MaxValue];

        private byte[] sendBodyBuffer = new byte[short.MaxValue];

        internal T unit { get; private set; }

        private bool isReceiveHead = true;

        private long lastTick;

        private bool isLagTest;

        private LagTest lagTest;

        internal void Init(Socket _socket, long _tick)
        {
            socket = _socket;

            lastTick = _tick;
        }

        internal void OpenLagTest(int _minLagTime, int _maxLagTime)
        {
            isLagTest = true;

            lagTest = new LagTest();

            lagTest.Init(socket, SendCallBack);

            lagTest.SetTime(_minLagTime, _maxLagTime);
        }

        internal void SetUnit(T _unit, long _tick)
        {
            unit = _unit;

            unit.Init(SendData);

            lastTick = _tick;

            byte[] bytes = unit.Login();

            SendDataReal(Constant.PackageTag.LOGIN, bytes);
        }

        internal void Kick(bool _logout)
        {
            if (unit != null)
            {
                unit.Kick(_logout);
            }

            socket.Close();
        }

        internal int CheckLogin(long _tick)
        {
            if (_tick - lastTick > Server<T>.idleTick)
            {
                Kick(true);

                return -1;
            }
            else if (socket.Available >= Constant.UID_LENGTH)
            {
                socket.Receive(uidBuffer, Constant.UID_LENGTH, SocketFlags.None);

                int uid = BitConverter.ToInt32(uidBuffer, 0);

                if (uid < 1)
                {
                    Kick(true);

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

        internal bool Update(long _tick)
        {
            if (_tick - lastTick > Server<T>.idleTick)
            {
                Kick(true);

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

        private void ReceiveHead(long _tick)
        {
            if (socket.Available >= Constant.HEAD_LENGTH)
            {
                socket.Receive(headBuffer, Constant.HEAD_LENGTH, SocketFlags.None);

                isReceiveHead = false;

                bodyLength = BitConverter.ToUInt16(headBuffer, 0);

                ReceiveBody(_tick);
            }
        }

        private void ReceiveBody(long _tick)
        {
            if (socket.Available >= bodyLength)
            {
                lastTick = _tick;

                socket.Receive(receiveBodyBuffer, bodyLength, SocketFlags.None);

                isReceiveHead = true;

                unit.ReceiveData(receiveBodyBuffer);

                ReceiveHead(_tick);
            }
        }

        internal void SendData(bool _isPush, MemoryStream _ms)
        {
            Constant.PackageTag tag = _isPush ? Constant.PackageTag.PUSH : Constant.PackageTag.REPLY;

            SendDataReal(tag, _ms);
        }

        private void SendDataReal(Constant.PackageTag _packageTag, MemoryStream _ms)
        {
            int length = Constant.HEAD_LENGTH + Constant.TYPE_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)(Constant.TYPE_LENGTH + _ms.Length)), sendBodyBuffer, Constant.HEAD_LENGTH);

            sendBodyBuffer[Constant.HEAD_LENGTH] = (byte)_packageTag;

            Array.Copy(_ms.GetBuffer(), 0, sendBodyBuffer, Constant.HEAD_LENGTH + Constant.TYPE_LENGTH, _ms.Length);

            if (!isLagTest)
            {
                try
                {
                    socket.BeginSend(sendBodyBuffer, 0, length, SocketFlags.None, SendCallBack, null);
                }
                catch (Exception e)
                {

                }
            }
            else
            {
                byte[] copy = new byte[length];

                Array.Copy(sendBodyBuffer, copy, length);

                lagTest.Add(copy);
            }
        }

        private void SendDataReal(Constant.PackageTag _packageTag, byte[] _bytes)
        {
            int length = Constant.HEAD_LENGTH + Constant.TYPE_LENGTH + _bytes.Length;

            Array.Copy(BitConverter.GetBytes((ushort)(Constant.TYPE_LENGTH + _bytes.Length)), sendBodyBuffer, Constant.HEAD_LENGTH);

            sendBodyBuffer[Constant.HEAD_LENGTH] = (byte)_packageTag;

            Array.Copy(_bytes, 0, sendBodyBuffer, Constant.HEAD_LENGTH + Constant.TYPE_LENGTH, _bytes.Length);

            try
            {
                socket.BeginSend(sendBodyBuffer, 0, length, SocketFlags.None, SendCallBack, null);
            }
            catch (Exception e)
            {

            }
        }

        private void SendCallBack(IAsyncResult _result)
        {
            try
            {
                socket.EndSend(_result);
            }
            catch (Exception e)
            {

            }
        }
    }
}