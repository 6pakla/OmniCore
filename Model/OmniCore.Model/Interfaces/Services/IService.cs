﻿using System.Threading;
using System.Threading.Tasks;
using OmniCore.Model.Interfaces.Common;
using OmniCore.Model.Interfaces.Services.Internal;

namespace OmniCore.Model.Interfaces.Services
{
    public interface IService : INotifyStatus
    {
        bool IsStarted { get; }
        bool IsPaused { get; }
        void RegisterDependentServices(params IService[] dependentServices);
        Task StartService(CancellationToken cancellationToken);
        Task OnBeforeStopRequest();
        Task StopService(CancellationToken cancellationToken);
        Task PauseService(CancellationToken cancellationToken);
        Task ResumeService(CancellationToken cancellationToken);
    }
}