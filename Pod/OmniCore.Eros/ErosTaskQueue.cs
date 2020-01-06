﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nito.AsyncEx;
using OmniCore.Model.Interfaces;

namespace OmniCore.Eros
{
    public class ErosTaskQueue : ITaskQueue
    {
        private ConcurrentQueue<ITask> Tasks;
        private Task QueueTask = Task.CompletedTask;
        private bool IsShuttingDown = false;

        public void Startup()
        {
            Tasks = new ConcurrentQueue<ITask>();
        }

        public void Shutdown()
        {
            IsShuttingDown = true;
            QueueTask.Wait();
        }

        public IEnumerable<ITask> List()
        {
            return Tasks.AsEnumerable();
        }

        public void Enqueue(ITask task)
        {
            Tasks.Enqueue(task);
            QueueTask.ContinueWith(_ => GetNext());
        }

        private Task GetNext()
        {
            if (!IsShuttingDown && Tasks.TryDequeue(out var erosTask))
            {
                return new Task(() =>
                    {
                    });
            }
            else
            {
                return Task.CompletedTask;
            }
        }
    }
}
