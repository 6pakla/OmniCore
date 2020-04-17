﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniCore.Model.Interfaces.Common;
using OmniCore.Model.Interfaces.Services.Facade;

namespace OmniCore.Model.Interfaces.Services.Internal
{
    public interface IDashPodProvider : IServiceInstance
    {
        Task<IList<IErosPod>> ActivePods(CancellationToken cancellationToken);
    }
}