﻿using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using OmniCore.Client.Models;
using OmniCore.Client.ViewModels.Base;
using OmniCore.Client.Views.Home;
using OmniCore.Model.Interfaces.Client;
using OmniCore.Model.Interfaces.Services.Facade;
using OmniCore.Model.Utilities.Extensions;
using Xamarin.Forms;

namespace OmniCore.Client.ViewModels.Home
{
    public class RadiosViewModel : BaseViewModel
    {
        public RadiosViewModel(ICoreClient client) : base(client)
        {
            SelectCommand = new Command<RadioModel>(async rm => await SelectRadio(rm.Radio));
            AddCommand = new Command(async _ =>
            {
                await Shell.Current.Navigation.PushAsync(Client.ViewPresenter.GetView<RadioScanView>(false));
            });
        }

        public ObservableCollection<RadioModel> Radios { get; set; }
        public ICommand SelectCommand { get; set; }
        public ICommand AddCommand { get; set; }

        protected override Task OnPageAppearing()
        {
            Radios = new ObservableCollection<RadioModel>();
            Api.PodService.ListErosRadios()
                .ObserveOn(Client.SynchronizationContext)
                .Subscribe(radio => { Radios.Add(new RadioModel(radio)); })
                .AutoDispose(this);

            return Task.CompletedTask;
        }

        private async Task SelectRadio(IRadio radio)
        {
            await Shell.Current.Navigation.PushAsync(Client.ViewPresenter.GetView<RadioDetailView>(false, radio));
        }
    }
}