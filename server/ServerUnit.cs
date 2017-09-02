﻿using System;
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

            byte[] bytes = unit.Login();

            SendDataReal(Constant.PackageTag.LOGIN, bytes);
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
            if (_tick - lastTick > Constant.KICK_TICK_LONG)
            {
                Kick();

                return -1;
            }
            else if (socket.Available >= Constant.UID_LENGTH)
            {
                socket.Receive(uidBuffer, Constant.UID_LENGTH, SocketFlags.None);

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
            if (_tick - lastTick > Constant.KICK_TICK_LONG)
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
            if (socket.Available >= Constant.HEAD_LENGTH)
            {
                socket.Receive(headBuffer, Constant.HEAD_LENGTH, SocketFlags.None);

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
            Constant.PackageTag tag = _isPush ? Constant.PackageTag.PUSH : Constant.PackageTag.REPLY;

            SendDataReal(tag, _ms);
        }

        private void SendDataReal(Constant.PackageTag _packageTag, MemoryStream _ms)
        {
            int length = Constant.HEAD_LENGTH + Constant.TYPE_LENGTH + (int)_ms.Length;

            Array.Copy(BitConverter.GetBytes((ushort)(Constant.TYPE_LENGTH + _ms.Length)), bodyBuffer, Constant.HEAD_LENGTH);

            bodyBuffer[Constant.HEAD_LENGTH] = (byte)_packageTag;

            Array.Copy(_ms.GetBuffer(), 0, bodyBuffer, Constant.HEAD_LENGTH + Constant.TYPE_LENGTH, _ms.Length);

            socket.BeginSend(bodyBuffer, 0, length, SocketFlags.None, SendCallBack, null);
        }

        private void SendDataReal(Constant.PackageTag _packageTag, byte[] _bytes)
        {
            int length = Constant.HEAD_LENGTH + Constant.TYPE_LENGTH + _bytes.Length;

            Array.Copy(BitConverter.GetBytes((ushort)(Constant.TYPE_LENGTH + _bytes.Length)), bodyBuffer, Constant.HEAD_LENGTH);

            bodyBuffer[Constant.HEAD_LENGTH] = (byte)_packageTag;

            Array.Copy(_bytes, 0, bodyBuffer, Constant.HEAD_LENGTH + Constant.TYPE_LENGTH, _bytes.Length);

            socket.BeginSend(bodyBuffer, 0, length, SocketFlags.None, SendCallBack, null);
        }

        private void SendCallBack(IAsyncResult _result)
        {
            socket.EndSend(_result);
        }
    }
}