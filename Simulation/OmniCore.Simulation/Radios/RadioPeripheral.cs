﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OmniCore.Model.Interfaces.Platform;

namespace OmniCore.Simulation.Radios
{
    public class RadioPeripheral : IRadioPeripheral
    {
        public void Dispose()
        {
        }

        public Guid PeripheralUuid { get; }
        public string PeripheralName { get; }
        public async Task<IRadioPeripheralLease> Lease(CancellationToken cancellationToken)
        {
            return new RadioPeripheralLease();
        }

        public TimeSpan? RssiUpdateTimeSpan { get; set; }
        public int? Rssi { get; set; }
        public DateTimeOffset? RssiDate { get; }
        public DateTimeOffset? LastSeen { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}