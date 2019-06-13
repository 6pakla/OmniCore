﻿using OmniCore.Model.Enums;
using OmniCore.Model.Interfaces;
using OmniCore.Model.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Xamarin.Forms;

namespace OmniCore.Mobile.ViewModels
{
    public class PodStatusViewModel : BaseViewModel
    {
        public PodStatusViewModel()
        {
        }

        private void StartTimer()
        {
            if (Pod != null && Pod.LastStatus != null && Pod.LastStatus.Progress >= PodProgress.Running)
            {
                Pod.LastStatus.UpdateWithEstimates(Pod);
                Device.StartTimer(TimeSpan.FromSeconds(15), () =>
                {
                    if (Pod != null && Pod.LastStatus != null && Pod.LastStatus.Progress >= PodProgress.Running)
                    {
                        Pod.LastStatus.UpdateWithEstimates(Pod);
                        return true;
                    }
                    else
                        return false;
                });
            }
        }

        private bool updateButtonEnabled = false;
        public bool UpdateButtonEnabled
        {
            get { return updateButtonEnabled; }
            set { SetProperty(ref updateButtonEnabled, value); }
        }

        protected override void OnPodPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateButtonEnabled = (Pod != null);
            Pod?.LastStatus?.UpdateWithEstimates(Pod);

            if (string.IsNullOrEmpty(e.PropertyName))
                OnPropertyChanged(string.Empty);
            else
            {
                if (e.PropertyName == nameof(IPod.Lot) || e.PropertyName == nameof(IPod.Serial)
                    || e.PropertyName == nameof(IPod.RadioAddress))
                    OnPropertyChanged(nameof(Id));

                if (e.PropertyName == nameof(IPod.ReservoirUsedForPriming))
                {
                    OnPropertyChanged(nameof(ReservoirColor));
                    OnPropertyChanged(nameof(ReservoirDelivered));
                    OnPropertyChanged(nameof(ReservoirRemaining));
                }

                if (e.PropertyName == nameof(IPod.LastStatus))
                {
                    OnPropertyChanged(nameof(Updated));
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(LifetimeActive));
                    OnPropertyChanged(nameof(LifetimeRemaining));
                    OnPropertyChanged(nameof(LifetimeProgress));
                    OnPropertyChanged(nameof(LifetimeColor));
                    OnPropertyChanged(nameof(ReservoirDelivered));
                    OnPropertyChanged(nameof(ReservoirRemaining));
                    OnPropertyChanged(nameof(ReservoirProgress));
                    OnPropertyChanged(nameof(ReservoirColor));
                }
            }
        }

        public string Id
        {
            get
            {
                if (Pod == null)
                    return $"No active pod";
                else if (!Pod.Lot.HasValue)
                    return $"R0x{Pod.RadioAddress:X8}";
                else
                    return $"L{Pod.Lot} T{Pod.Serial} R0x{Pod.RadioAddress:X8}";
            }
        }

        public string Updated
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null)
                {
                    var updated = DateTime.UtcNow - Pod.LastStatus.Created;
                    if (updated.TotalSeconds < 15)
                        return $"Just now";
                    else if (updated.TotalMinutes < 1)
                        return $"Less than a minute ago";
                    else if (updated.TotalMinutes < 2)
                        return $"1 minute ago";
                    else if (updated.TotalMinutes < 3)
                        return $"2 minutes ago";
                    else if (updated.TotalMinutes < 60)
                        return $"{(int)updated.TotalMinutes} minutes ago";
                    else if (updated.TotalMinutes < 120)
                    {
                        if (updated.TotalMinutes < 70)
                            return $"1 hour ago";
                        else
                            return $"more than 1 hour ago";
                    }
                    else if (updated.TotalHours < 24)
                        return $"{(int)updated.TotalHours} hours ago";
                    else if (updated.TotalHours < 25)
                        return $"1 day ago";
                    else if (updated.TotalHours < 48)
                        return $"More than 1 day ago";
                    else
                        return $"{(int)updated.TotalDays} days ago";
                }
                else
                    return "Not yet updated";
            }
        }

        public string Status
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null)
                {
                    switch(Pod.LastStatus.Progress)
                    {
                        case PodProgress.InitialState:
                        case PodProgress.TankPowerActivated:
                        case PodProgress.TankFillCompleted:
                            return $"Not yet paired";
                        case PodProgress.PairingSuccess:
                            return $"Paired";
                        case PodProgress.Purging:
                            return $"Priming";
                        case PodProgress.ReadyForInjection:
                            return $"Ready for Insertion";
                        case PodProgress.BasalScheduleSet:
                        case PodProgress.Priming:
                            return $"Starting";
                        case PodProgress.Running:
                            return $"Running";
                        case PodProgress.RunningLow:
                            return $"Running (Low Reservoir)";
                        case PodProgress.ErrorShuttingDown:
                            return $"Error";
                        case PodProgress.AlertExpiredShuttingDown:
                            return $"Expired";
                        case PodProgress.Inactive:
                            return $"Deactivated";
                        default:
                            return "Unknown";
                    }
                }
                else
                    return "Unknown";
            }
        }

        public string LifetimeRemaining
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null)
                {
                    var ts = TimeSpan.FromMinutes(4800 - Pod.LastStatus.ActiveMinutesEstimate);
                    return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
                }
                else
                    return "Unknown";
            }
        }

        public string LifetimeActive
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null)
                {
                    var ts = TimeSpan.FromMinutes(Pod.LastStatus.ActiveMinutesEstimate);
                    return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
                }
                else
                    return "Unknown";
            }
        }

        public Color LifetimeColor
        {
            get
            {
                if (Pod == null)
                    return Color.Beige;
                else if (Pod.LastStatus != null)
                {
                    var mins = Pod.LastStatus.ActiveMinutesEstimate;
                    if (mins < 24 * 60 * 3)
                        return Color.LightGreen;
                    if (mins < 24 * 60 * 3)
                        return Color.Green;
                    if (mins < 24 * 60 * 3)
                        return Color.GreenYellow;

                    return Color.IndianRed;
                }
                else
                    return Color.Beige;
            }
        }

        public double LifetimeProgress
        {
            get
            {
                if (Pod == null)
                    return 0;
                else if (Pod.LastStatus != null)
                {
                    if (Pod.LastStatus.ActiveMinutes >= 4800)
                        return 0;
                    return ((4800 - Pod.LastStatus.ActiveMinutesEstimate) / 4800.0);
                }
                else
                    return 0;
            }
        }

        public string ReservoirRemaining
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null)
                {
                    if (Pod.LastStatus.Progress < PodProgress.RunningLow)
                        return "More than 50U";

                    return $"{Pod.LastStatus.ReservoirEstimate}U";
                }
                else
                    return "Unknown";
            }
        }

        public string ReservoirDelivered
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null)
                {
                    if (Pod.ReservoirUsedForPriming.HasValue)
                        return $"{Pod.LastStatus.DeliveredInsulinEstimate - Pod.ReservoirUsedForPriming.Value}U";
                    else
                        return $"{Pod.LastStatus.DeliveredInsulinEstimate - 2.65m}U";
                }
                else
                    return "Unknown";
            }
        }

        public Color ReservoirColor
        {
            get
            {
                if (Pod == null)
                    return Color.Beige;
                else if (Pod.LastStatus != null)
                {
                    if (Pod.LastStatus.Progress < PodProgress.RunningLow)
                        return Color.LightGreen;

                    if (Pod.LastStatus.ReservoirEstimate < 10)
                        return Color.Red;
                    else
                        return Color.Yellow;
                }
                else
                    return Color.Beige;
            }
        }

        public double ReservoirProgress
        {
            get
            {
                if (Pod == null)
                    return 0;
                else if (Pod.LastStatus != null)
                {
                    if (Pod.LastStatus.ReservoirEstimate >= 50m)
                        return 1;
                    return (double)Pod.LastStatus.ReservoirEstimate / 50.0;
                }
                else
                    return 0;
            }
        }

        public string BasalStatus
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null)
                {
                    if (Pod.LastStatus.BasalStateEstimate == BasalState.Scheduled)
                        return "Basal Active";
                    else
                    {
                        if (Pod.LastStatus.TemporaryBasalRate.HasValue && Pod.LastStatus.TemporaryBasalRate.Value == 0m)
                        {
                            return "Basal Suspended";
                        }
                        return "Temporary Basal Active";
                    }
                }
                else
                    return string.Empty;
            }
        }

        public string BasalText1
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null && Pod.LastStatus.BasalStateEstimate == BasalState.Scheduled)
                {
                    if (Pod.LastStatus.ScheduledBasalRate.HasValue)
                        return $"{Pod.LastStatus.ScheduledBasalRate} U/h";
                    else
                        return $"Rate unknown";
                }
                else if (Pod.LastStatus != null && Pod.LastStatus.BasalStateEstimate == BasalState.Temporary)
                {
                    if (Pod.LastStatus.TemporaryBasalRate.HasValue)
                    {
                        if (Pod.LastStatus.TemporaryBasalTotalHours == 0.5m)
                            return $"{Pod.LastStatus.TemporaryBasalRate} for {Pod.LastStatus.TemporaryBasalTotalHours}";
                        else if (Pod.LastStatus.TemporaryBasalTotalHours == 1m)
                            return $"{Pod.LastStatus.TemporaryBasalRate} for {Pod.LastStatus.TemporaryBasalTotalHours}";
                        else if (Pod.LastStatus.TemporaryBasalTotalHours == 1m)
                            return $"{Pod.LastStatus.TemporaryBasalRate} for {Pod.LastStatus.TemporaryBasalTotalHours}";
                    }
                    return $"Rate and duration unknown";
                }
                else
                    return string.Empty;
            }
        }

        public string BasalText2
        {
            get
            {
                if (Pod == null)
                    return string.Empty;
                else if (Pod.LastStatus != null && Pod.LastStatus.BasalStateEstimate == BasalState.Scheduled
                    && Pod.LastStatus.ScheduledBasalAverage.HasValue)
                {
                    return $"(Daily average {Pod.LastStatus.ScheduledBasalAverage} U/h)";
                }
                else if (Pod.LastStatus != null && Pod.LastStatus.BasalStateEstimate == BasalState.Temporary
                    && Pod.LastStatus.TemporaryBasalRemaining.HasValue)
                {
                    var remaining = Pod.LastStatus.TemporaryBasalRemaining.Value;

                    if (remaining.TotalMinutes < 1)
                        return $"(Less than a minute remaining)";
                    else if (remaining.TotalMinutes < 2)
                        return $"(1 minute remaining)";
                    else if (remaining.TotalMinutes < 120)
                        return $"({(int)remaining.TotalMinutes} minutes remaining)";
                    else if (remaining.Minutes == 0)
                        return $"({remaining.Hours} hours remaining)";
                    else if (remaining.Minutes == 1)
                        return $"({remaining.Hours} hours and 1 minute remaining)";
                    else
                        return $"({remaining.Hours} hours and {remaining.Minutes} minute remaining)";
                }
                else
                    return string.Empty;
            }
        }
    }
}