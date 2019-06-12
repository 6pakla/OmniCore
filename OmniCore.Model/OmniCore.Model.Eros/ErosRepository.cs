﻿using OmniCore.Model.Interfaces;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SQLiteNetExtensions.Extensions;
using System.Threading.Tasks;
using OmniCore.Model.Interfaces.Data;
using OmniCore.Model.Eros.Data;

namespace OmniCore.Model.Eros
{
    public class ErosRepository
    {
        private static readonly ErosRepository instance = new ErosRepository();
        public static ErosRepository Instance
        {
            get
            {
                return instance;
            }
        }

        private readonly string DbPath;
        //private string DbConnectionString;

        private ErosRepository()
        {
            DbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "omnicore.db3");
            //DbConnectionString = $"Data Source={DbPath}";
            Initialize();
        }

        public void Initialize()
        {
            try
            {
                using (var conn = new SQLiteConnection(DbPath))
                {
                    conn.BeginTransaction();
                    conn.CreateTable<ErosPod>();
                    conn.CreateTable<ErosAlertStates>();
                    conn.CreateTable<ErosBasalSchedule>();
                    conn.CreateTable<ErosFault>();
                    conn.CreateTable<ErosStatus>();
                    conn.CreateTable<ErosUserSettings>();
                    conn.CreateTable<ErosMessageExchangeParameters>();
                    conn.CreateTable<ErosMessageExchangeResult>();
                    conn.CreateTable<ErosMessageExchangeStatistics>();
                    conn.Commit();
                }
            }
            catch (SQLiteException sle)
            {
                Console.WriteLine($"Error: {sle}");
                throw sle;
            }
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(DbPath);
        }

        public ErosPod LoadCurrent()
        {
            using (var conn = GetConnection())
            {
                return WithRelations(conn.Table<ErosPod>()
                    .FirstOrDefault(x => !x.Archived), conn);
            }
        }

        public ErosPod Load(uint lot, uint tid)
        {
            using (var conn = GetConnection())
            {
                return WithRelations(conn.Table<ErosPod>()
                    .FirstOrDefault(x => x.Lot == lot && x.Serial == tid), conn);
            }
        }

        public ErosPod GetLastActivated()
        {
            using (var conn = GetConnection())
            {
                return WithRelations(conn.Table<ErosPod>().OrderByDescending(x => x.ActivationDate)
                    .FirstOrDefault(), conn);
            }
        }

        public void Save(IPod pod, IMessageExchangeResult result = null)
        {
            using (var conn = new SQLiteConnection(DbPath))
            {
                conn.BeginTransaction();
                conn.InsertOrReplace(pod);

                if (result != null)
                {
                    result.PodId = pod.Id.Value;

                    if (result.Statistics != null)
                    {
                        result.Statistics.PodId = pod.Id.Value;
                        result.Statistics.Id = conn.InsertOrReplace(result.Statistics, typeof(ErosMessageExchangeStatistics));
                        result.StatisticsId = result.Statistics.Id;
                    }

                    if (result.ExchangeParameters != null)
                    {
                        result.ExchangeParameters.PodId = pod.Id.Value;
                        result.ExchangeParameters.Created = DateTime.UtcNow;
                        result.ExchangeParameters.Id = conn.InsertOrReplace(result.ExchangeParameters, typeof(ErosMessageExchangeParameters));
                        result.ParametersId = result.ExchangeParameters.Id;
                    }

                    if (result.AlertStates != null)
                    {
                        result.AlertStates.PodId = pod.Id.Value;
                        result.AlertStates.Created = DateTime.UtcNow;
                        result.AlertStates.Id = conn.InsertOrReplace(result.AlertStates, typeof(ErosAlertStates));
                        result.AlertStatesId = result.AlertStates.Id;
                        pod.LastAlertStates = result.AlertStates;
                    }

                    if (result.BasalSchedule != null)
                    {
                        result.BasalSchedule.PodId = pod.Id.Value;
                        result.BasalSchedule.Created = DateTime.UtcNow;
                        result.BasalSchedule.Id = conn.InsertOrReplace(result.BasalSchedule, typeof(ErosBasalSchedule));
                        result.BasalScheduleId = result.BasalSchedule.Id;
                        pod.LastBasalSchedule = result.BasalSchedule;
                    }

                    if (result.Fault != null)
                    {
                        result.Fault.PodId = pod.Id.Value;
                        result.Fault.Created = DateTime.UtcNow;
                        result.Fault.Id = conn.InsertOrReplace(result.Fault, typeof(ErosFault));
                        result.FaultId = result.Fault.Id;
                        pod.LastFault = result.Fault;
                    }

                    if (result.Status != null)
                    {
                        result.Status.PodId = pod.Id.Value;
                        result.Status.Created = DateTime.UtcNow;
                        result.Status.Id = conn.InsertOrReplace(result.Status, typeof(ErosStatus));
                        result.StatusId = result.Status.Id;
                        pod.LastStatus = result.Status;
                    }

                    if (result.UserSettings != null)
                    {
                        result.UserSettings.PodId = pod.Id.Value;
                        result.UserSettings.Created = DateTime.UtcNow;
                        result.UserSettings.Id = conn.InsertOrReplace(result.UserSettings, typeof(ErosUserSettings));
                        result.UserSettingsId = result.UserSettings.Id;
                        pod.LastUserSettings = result.UserSettings;
                    }

                    result.Id = conn.InsertOrReplace(result, typeof(ErosMessageExchangeResult));
                }

                conn.Commit();
            }
        }

        public List<ErosMessageExchangeResult> GetResults(int startAfterId)
        {
            using (var conn = GetConnection())
            {
                return conn.GetAllWithChildren<ErosMessageExchangeResult>
                    (x => x.Id > startAfterId && x.Success)
                    .OrderBy(x => x.Id)
                    .ToList();
            }
        }

        private ErosPod WithRelations(ErosPod pod, SQLiteConnection conn)
        {
            if (pod == null)
                return null;

            pod.LastAlertStates = conn.Table<ErosAlertStates>().Where(x => x.PodId == pod.Id).OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastBasalSchedule = conn.Table<ErosBasalSchedule>().Where(x => x.PodId == pod.Id).OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastFault = conn.Table<ErosFault>().Where(x => x.PodId == pod.Id).OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastStatus = conn.Table<ErosStatus>().Where(x => x.PodId == pod.Id).OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastUserSettings = conn.Table<ErosUserSettings>().Where(x => x.PodId == pod.Id).OrderByDescending(x => x.Id)
                .FirstOrDefault();

            return pod;
        }
    }
}
