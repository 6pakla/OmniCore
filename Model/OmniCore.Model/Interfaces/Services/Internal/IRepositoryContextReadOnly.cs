﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OmniCore.Model.Entities;
using OmniCore.Model.Interfaces.Common;

namespace OmniCore.Model.Interfaces.Services.Internal
{
    public interface IRepositoryContextReadOnly : IDisposable, IServerResolvable
    {
        void SetLock(IDisposable readerWriterLock, bool tracking);
        DbSet<MedicationEntity> Medications { get; }
        DbSet<UserEntity> Users { get; }
        DbSet<RadioEntity> Radios { get; }
        DbSet<RadioEventEntity> RadioEvents { get; }
        DbSet<PodEntity> Pods { get; }
        DbSet<PodRequestEntity> PodRequests { get; }
        DbSet<MedicationDeliveryEntity> MedicationDeliveries { get; }
        DbSet<PodResponseEntity> PodResponses { get; }
        Task InitializeDatabase(CancellationToken cancellationToken, bool createNew = false);
    }
}