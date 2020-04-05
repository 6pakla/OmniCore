﻿namespace OmniCore.Model.Interfaces.Common
{
    public interface ICoreContainer<in TResolvable> : IServerResolvable, IClientResolvable
        where TResolvable : IResolvable
    {
        ICoreContainer<TResolvable> Many<T>() where T : TResolvable;
        ICoreContainer<TResolvable> Many<TI, TC>() where TC : TI where TI : TResolvable;
        ICoreContainer<TResolvable> One<T>() where T : TResolvable;
        ICoreContainer<TResolvable> One<TI, TC>() where TC : TI where TI : TResolvable;
        ICoreContainer<TResolvable> One<TI, TC>(string discriminator) where TC : TI where TI : TResolvable;
        ICoreContainer<TResolvable> Existing<T>(T instance) where T : TResolvable;
        T Get<T>() where T : TResolvable;
        T[] GetAll<T>() where T : TResolvable;
    }
}