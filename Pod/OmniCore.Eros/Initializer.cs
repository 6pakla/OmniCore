﻿using OmniCore.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using OmniCore.Model.Constants;
using OmniCore.Model.Interfaces.Data;

namespace OmniCore.Eros
{
    public static class Initializer
    {
        public static ICoreContainer<IServerResolvable> WithOmnipodEros
            (this ICoreContainer<IServerResolvable> container)
        {
            return container
                .One<IPodService, ErosPodService>()
                .Many<IPod, ErosPod>()
                .Many<IPodRequest, ErosPodRequest>()
                .Many<ITaskQueue, ErosPodRequestQueue>();
        }
    }
}

