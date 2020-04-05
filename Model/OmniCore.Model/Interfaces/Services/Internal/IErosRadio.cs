﻿using System;
using System.Threading;
using System.Threading.Tasks;
using OmniCore.Model.Entities;
using OmniCore.Model.Interfaces.Common;
using OmniCore.Model.Interfaces.Services.Facade;

namespace OmniCore.Model.Interfaces.Services.Internal
{
    public interface IErosRadio : IRadio, IServerResolvable, IDisposable
    {
        RadioOptions Options { get; }
        void StartMonitoring();

        Task<byte[]> GetResponse(IErosPodRequest request, CancellationToken cancellationToken, RadioOptions options);
        // Task<(byte Rssi, byte[] Data)> DebugGetPacket(uint timeoutMilliseconds, CancellationToken cancellationToken);
    }
}