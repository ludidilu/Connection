using System;
using System.Collections.Generic;
using System.Threading;

namespace superService
{
    public class SuperService
    {
        private readonly object processLocker = new object();

        private readonly object callLocker = new object();

        private bool inProcessQueue = false;//判断是否已经召唤线程去执行process队列了  使用这个属性一定要持有processLocker的锁

        private int callNum = 0;//记录call的等待数量  使用这个属性一定要持有processLocker的锁

        private Queue<Action> processActionList = new Queue<Action>();

        public void Process(Action _action)
        {
            bool needCallThread = false;

            lock (processLocker)
            {
                processActionList.Enqueue(_action);

                if (!inProcessQueue && callNum == 0)
                {
                    inProcessQueue = true;

                    needCallThread = true;
                }
            }

            if (needCallThread)
            {
                ThreadPool.QueueUserWorkItem(StartProcess);
            }
        }

        private void BeginCall()
        {
            lock (processLocker)
            {
                callNum++;
            }
        }

        private void EndCall()
        {
            bool needCallThread = false;

            lock (processLocker)
            {
                callNum--;

                if (!inProcessQueue && callNum == 0 && processActionList.Count > 0)
                {
                    inProcessQueue = true;

                    needCallThread = true;
                }
            }

            if (needCallThread)
            {
                ThreadPool.QueueUserWorkItem(StartProcess);
            }
        }

        public void Call(Action _action)
        {
            BeginCall();

            lock (callLocker)
            {
                _action();
            }

            EndCall();
        }

        public R Call<R>(Func<R> _action)
        {
            R result;

            BeginCall();

            lock (callLocker)
            {
                result = _action();
            }

            EndCall();

            return result;
        }

        private void StartProcess(object _param)
        {
            while (true)
            {
                Action action;

                lock (processLocker)
                {
                    if (processActionList.Count == 0 || callNum > 0)
                    {
                        inProcessQueue = false;

                        return;
                    }

                    action = processActionList.Dequeue();
                }

                lock (callLocker)
                {
                    action();
                }
            }
        }
    }
}
