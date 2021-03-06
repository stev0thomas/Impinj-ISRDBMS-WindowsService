﻿// Copyright ©2018 Impinj, Inc. All rights reserved.
// You may use and modify this code under the terms of the Impinj Software Tools License & Disclaimer.
// Visit https://support.impinj.com/hc/en-us/articles/360000468370-Software-Tools-License-Disclaimer
// for full license details, or contact Impinj, Inc. at support@impinj.com for a copy of the license.

//#define DEBUG
#undef DEBUG

using System;
using System.Configuration;
using System.Collections;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace ItemSenseRDBMService
{
    class SQLServerRDBMS : RDBMSbase
    {
        protected static DataTable dtEvents = null;

        public SQLServerRDBMS(ArrayList mastEvent, ArrayList itemEvent, ArrayList thr, ArrayList itemFile) : base(mastEvent, itemEvent, thr, itemFile)
        {
            #region DataTable Setup definitions

            dtEvents = new DataTable("epc_master_events");

            DataColumn epcColumn = new DataColumn();
            epcColumn.DataType = typeof(String);
            epcColumn.ColumnName = "epc_nbr";
            dtEvents.Columns.Add(epcColumn);

            DataColumn obsTmColumn = new DataColumn();
            obsTmColumn.DataType = typeof(DateTime);
            obsTmColumn.ColumnName = "last_obsv_time";
            dtEvents.Columns.Add(obsTmColumn);

            DataColumn tagIdColumn = new DataColumn();
            tagIdColumn.DataType = typeof(String);
            tagIdColumn.ColumnName = "tag_id";
            dtEvents.Columns.Add(tagIdColumn);

            DataColumn ZoneColumn = new DataColumn();
            ZoneColumn.DataType = typeof(String);
            ZoneColumn.ColumnName = "zone_name";
            dtEvents.Columns.Add(ZoneColumn);

            DataColumn FloorColumn = new DataColumn();
            FloorColumn.DataType = typeof(String);
            FloorColumn.ColumnName = "floor";
            dtEvents.Columns.Add(FloorColumn);

            DataColumn FacColumn = new DataColumn();
            FacColumn.DataType = typeof(String);
            FacColumn.ColumnName = "facility";
            dtEvents.Columns.Add(FacColumn);

            DataColumn XColumn = new DataColumn();
            XColumn.DataType = typeof(double);
            XColumn.ColumnName = "x_coord";
            dtEvents.Columns.Add(XColumn);

            DataColumn YColumn = new DataColumn();
            YColumn.DataType = typeof(double);
            YColumn.ColumnName = "y_coord";
            dtEvents.Columns.Add(YColumn);

            DataColumn UpcColumn = new DataColumn();
            UpcColumn.DataType = typeof(String);
            UpcColumn.ColumnName = "upc_nbr";
            dtEvents.Columns.Add(UpcColumn);

            DataColumn updTmColumn = new DataColumn();
            updTmColumn.DataType = typeof(DateTime);
            updTmColumn.ColumnName = "last_updt_time";
            dtEvents.Columns.Add(updTmColumn);

            #endregion
        }

        protected override void WriteRawItemEventRecordsToRDBMS()
        {
            #if (DEBUG)            
            #region debug_WriteRawItemEventRecordsToRDBMS_kpi
            DateTime blockTmSt = System.DateTime.Now;
            iLog.WriteEntry("WriteRawItemEventRecordsToRDBMS started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region SqlServer DDL
            //Do Not Alter - These strings are modified via the app.cfg
            //Drop and Create "updatedb_cmd"
            const string cmdText = @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{is_raw_item_event_hist}' AND xtype = 'U') CREATE TABLE {is_raw_item_event_hist} " +
                @"(epc_nbr VARCHAR(128) NOT NULL, tag_id VARCHAR(128),  job_id VARCHAR(128), from_zone VARCHAR(128), from_floor VARCHAR(128), to_zone VARCHAR(128), " +
                @"to_floor VARCHAR(128), from_fac VARCHAR(128), to_fac VARCHAR(128), from_x float, from_y float, to_x float, " +
                @"to_y float, obsv_time DateTime, PRIMARY KEY(epc_nbr, obsv_time)); " +
                @"DROP TABLE IF EXISTS {is_raw_item_event}; CREATE TABLE {is_raw_item_event} (epc_nbr VARCHAR(128) NOT NULL, " +
                @"tag_id VARCHAR(128) NULL, job_id VARCHAR(128), from_zone VARCHAR(128), from_floor VARCHAR(128), " +
                @"to_zone VARCHAR(128), to_floor VARCHAR(128), from_fac VARCHAR(128), to_fac VARCHAR(128), " +
                @"from_x float, from_y float, to_x float, to_y float, obsv_time DateTime);";
            string rplTxt = cmdText.Replace("{is_raw_item_event_hist}", ConfigurationManager.AppSettings["ItemSenseRawItemEventHistTableName"]);
            string cfgCmdText = rplTxt.Replace("{is_raw_item_event}", ConfigurationManager.AppSettings["ItemSenseRawItemEventTableName"]);

            //Update History "updatedb_cmd"
            const string postCmdText = @"INSERT INTO {is_raw_item_event_hist} (epc_nbr, tag_id, job_id, from_zone, from_floor, to_zone, to_floor, from_fac, to_fac, from_x, from_y, to_x, to_y, obsv_time) " +
                @"SELECT epc_nbr, tag_id, job_id, from_zone, from_floor, to_zone, to_floor, from_fac, to_fac, from_x, from_y, to_x, to_y, obsv_time FROM {is_raw_item_event}; ";
            string postRplTxt = postCmdText.Replace("{is_raw_item_event_hist}", ConfigurationManager.AppSettings["ItemSenseRawItemEventHistTableName"]);
            string postCfgCmdText = postRplTxt.Replace("{is_raw_item_event}", ConfigurationManager.AppSettings["ItemSenseRawItemEventTableName"]);

#endregion

            string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
            System.Data.DataTableReader reader = rawItemEventRecs.CreateDataReader();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    SqlCommand createdb_cmd = new SqlCommand(cfgCmdText, conn);
                    //Create history table if necessary, drop and recreate the temporary raw_item_event table
                    createdb_cmd.ExecuteNonQuery();

                    //insert into the raw_item_event table events just read
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn))
                    {
                        bulkCopy.DestinationTableName = ConfigurationManager.AppSettings["ItemSenseRawItemEventTableName"];
                        bulkCopy.WriteToServer(reader);
                    }

                    SqlCommand updatedb_cmd = new SqlCommand(postCfgCmdText, conn);
                    //update the raw_item_event_history table with whatever is in raw_item_event table
                    updatedb_cmd.ExecuteNonQuery();

                    if (Convert.ToBoolean(ConfigurationManager.AppSettings["ShowTagsInEventViewer"]))
                        iLog.WriteEntry("WriteRawItemEventRecordsToRDBMS rows inserted: " + itemEventRecords.Count.ToString(), EventLogEntryType.Information, eventId); eventId++;


                    if (Convert.ToBoolean(ConfigurationManager.AppSettings["SmoothDataXY"]))
                        WriteSmoothedItemEventRecordsToRDBMS();

                }
                catch (Exception ex)
                {
                    string errMsg = "WriteRawItemEventRecordsToRDBMS Exception: " + ex.Message + "(" + ex.GetType() + ")";
                    if (null != ex.InnerException)
                        errMsg += Environment.NewLine + ex.InnerException.Message;
                    iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
                }
                finally
                {
                    reader.Close();
                    conn.Close();
                }
            }

            #if (DEBUG)
            #region debug_WriteRawItemEventRecordsToRDBMS_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("WriteRawItemEventRecordsToRDBMS completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

        }


        /// <summary>
        /// This function relies on the database to do the moving average and weighted moving average calculations on X and Y coords
        /// of the reads.  The algorithm compares what was inserted into the raw_item_event table and compares with the 
        /// raw_item_event_hist table only timestamps for same epc_nbr in location read within the last 10 seconds in history table
        /// </summary>
        protected override void WriteSmoothedItemEventRecordsToRDBMS()
        {
            #if (DEBUG)
            #region debug_WriteSmoothedItemEventRecordsToRDBMS_kpi
            DateTime blockTmSt = System.DateTime.Now;
            iLog.WriteEntry("WriteSmoothedItemEventRecordsToRDBMS started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
#endregion
            #endif

            #region Postgresql DDL
            //Do Not Alter - These strings are modified via the app.cfg
            const string cmdText = @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{smoothed_item_event_hist}' AND xtype = 'U') CREATE TABLE {smoothed_item_event_hist} " +
                @"(epc_nbr VARCHAR(128) NOT NULL, fromX_ma float, fromY_ma float, toX_ma float, toY_ma float,  " +
                @"fromX_wma float, fromY_wma float, toX_wma float, toY_wma float, calc_time DateTime, PRIMARY KEY(epc_nbr, calc_time)); " +
                @"INSERT INTO {smoothed_item_event_hist} (epc_nbr, fromX_ma, fromY_ma, toX_ma, toY_ma, fromX_wma, fromY_wma, toX_wma, toY_wma, calc_time) " +
                @"SELECT DISTINCT z.epc_nbr, z.fromX_ma, z.fromY_ma, z.toX_ma, z.toY_ma, SUM(z.fromX_wma)/z.sum_weighted as fromX_wma, " +
                @"SUM(z.fromY_wma)/z.sum_weighted as fromY_wma, SUM(z.toX_wma)/z.sum_weighted as toX_wma, SUM(z.toY_wma)/z.sum_weighted as toY_wma, GETDATE() " +
                @"FROM (SELECT y.epc_nbr, AVG(y.from_x) as fromX_ma, AVG(y.from_y) as fromY_ma, AVG(y.to_x) as toX_ma, AVG(y.to_y) as toY_ma, " +
                @"SUM(CASE WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 2 THEN 0.5 " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 4 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 2 THEN 0.25 " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 6 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 4 THEN 0.18 " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 10 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 6 THEN 0.07 " +
                @"END) as sum_weighted, " +
                @"SUM(CASE WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 2 THEN 0.5 * y.from_x " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 4 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 2 THEN 0.25 * y.from_x " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 6 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 4 THEN 0.18 * y.from_x " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 10 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 6 THEN 0.07 * y.from_x " +
                @"END) as fromX_wma, " +
                @"SUM(CASE WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 2 THEN 0.5 * y.from_y " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 4 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 2 THEN 0.25 * y.from_y " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 6 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 4 THEN 0.18 * y.from_y " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 10 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 6 THEN 0.07 * y.from_y " +
                @"END) as fromY_wma, " +
                @"SUM(CASE WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 2 THEN 0.5 * y.to_x " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 4 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 2 THEN 0.25 * y.to_x " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 6 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 4 THEN 0.18 * y.to_x " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 10 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 6 THEN 0.07 * y.to_x " +
                @"END) as toX_wma, " +
                @"SUM(CASE WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 2 THEN 0.5 * y.to_y " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 4 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 2 THEN 0.25 * y.to_y " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 6 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 4 THEN 0.18 * y.to_y " +
                @"WHEN DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 10 AND DATEDIFF(Second, x.obsv_time, y.obsv_time) > 6 THEN 0.07 * y.to_y " +
                @"END) as toY_wma " +
                @"FROM {is_raw_item_event} x " +
                @"JOIN {is_raw_item_event_hist} y ON y.epc_nbr = x.epc_nbr AND DATEDIFF(Second, x.obsv_time, y.obsv_time) <= 10 AND y.obsv_time <= x.obsv_time " +
                @"GROUP BY y.epc_nbr) as z " +
                @"GROUP BY z.epc_nbr, z.fromX_ma, z.fromY_ma, z.toX_ma, z.toY_ma, z.fromX_wma, z.fromY_wma, z.toX_wma, z.toY_wma, z.sum_weighted " +
                @"ORDER BY z.epc_nbr; ";

            string rplTxt = cmdText.Replace("{smoothed_item_event_hist}", ConfigurationManager.AppSettings["SmoothedItemEventHistTableName"]);
            string cfgRplText = rplTxt.Replace("{is_raw_item_event}", ConfigurationManager.AppSettings["ItemSenseRawItemEventTableName"]);
            string cfgCmdText = cfgRplText.Replace("{is_raw_item_event_hist}", ConfigurationManager.AppSettings["ItemSenseRawItemEventHistTableName"]);


#endregion

            string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    SqlCommand updatedb_cmd = new SqlCommand(cfgCmdText, conn);
                    updatedb_cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    string errMsg = "WriteSmoothedItemEventRecordsToRDBMS Exception: " + ex.Message + "(" + ex.GetType() + ")";
                    if (null != ex.InnerException)
                        errMsg += Environment.NewLine + ex.InnerException.Message;
                    iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
                }
                finally
                {
                    conn.Close();
                }
            }

            #if (DEBUG)
            #region debug_WriteSmoothedItemEventRecordsToRDBMS_kpi
            DateTime procTmEnd = DateTime.Now;
            TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
            iLog.WriteEntry("WriteSmoothedItemEventRecordsToRDBMS completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
#endregion
            #endif
        }

        protected override void WriteThresholdRecordsToRDBMS()
        {
            #if (DEBUG)
            #region debug_WriteThresholdRecordsToRDBMS_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("WriteThresholdRecordsToRDBMS started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region SqlServer DDL
            //Do Not Alter - These strings are modified via the app.cfg
            //Drop and Create "updatedb_cmd"
            const string cmdText = @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{is_threshold_hist}' AND xtype = 'U') CREATE TABLE " +
                @"{is_threshold_hist} (epc_nbr VARCHAR(128) NOT NULL, observation_time DateTime, from_zone VARCHAR(128), to_zone VARCHAR(128)," +
                @"threshold VARCHAR(128), confidence float, job_id VARCHAR(128), job_name VARCHAR(128), PRIMARY KEY(epc_nbr, observation_time)); " +
                @"DROP TABLE IF EXISTS {is_threshold}; CREATE TABLE {is_threshold} (epc_nbr VARCHAR(128) NOT NULL, observation_time DateTime, " +
                @"from_zone VARCHAR(128), to_zone VARCHAR(128), threshold VARCHAR(128), confidence float, job_id VARCHAR(128), job_name VARCHAR(128));";
            string rplTxt = cmdText.Replace("{is_threshold_hist}", ConfigurationManager.AppSettings["ItemSenseThresholdHistTableName"]);
            string cfgCmdText = rplTxt.Replace("{is_threshold}", ConfigurationManager.AppSettings["ItemSenseThresholdTableName"]);

            //Update History "updatedb_cmd"
            const string postCmdText = @"INSERT INTO {is_threshold_hist} (epc_nbr, observation_time, from_zone, to_zone, threshold, confidence, job_id, job_name) " +
                @"SELECT epc_nbr, observation_time, from_zone, to_zone, threshold, confidence, job_id, job_name FROM {is_threshold}; ";
            string postRplTxt = postCmdText.Replace("{is_threshold_hist}", ConfigurationManager.AppSettings["ItemSenseThresholdHistTableName"]);
            string postCfgCmdText = postRplTxt.Replace("{is_threshold}", ConfigurationManager.AppSettings["ItemSenseThresholdTableName"]);


            #endregion

            string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
            System.Data.DataTableReader reader = thrRecs.CreateDataReader();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    SqlCommand createdb_cmd = new SqlCommand(cfgCmdText, conn);
                    //Create history table if necessary, drop and recreate the temporary threshold table
                    createdb_cmd.ExecuteNonQuery();

                    //Bulk insert into the threshold table events just read
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn))
                    {
                        bulkCopy.DestinationTableName = ConfigurationManager.AppSettings["ItemSenseThresholdTableName"];
                        bulkCopy.WriteToServer(reader);
                    }

                    SqlCommand updatedb_cmd = new SqlCommand(postCfgCmdText, conn);
                    //update the threshold_history table with whatever is in threshold table
                    updatedb_cmd.ExecuteNonQuery();

                    if (Convert.ToBoolean(ConfigurationManager.AppSettings["ShowTagsInEventViewer"]))
                        iLog.WriteEntry("WriteThresholdRecordsToRDBMS rows inserted: " + thrRecords.Count.ToString(), EventLogEntryType.Information, eventId); eventId++;

                }
                catch (Exception ex)
                {
                    string errMsg = "WriteThresholdRecordsToRDBMS Exception: " + ex.Message + "(" + ex.GetType() + ")";
                    if (null != ex.InnerException)
                        errMsg += Environment.NewLine + ex.InnerException.Message;
                    iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
                }
                finally
                {
                    reader.Close();
                    conn.Close();
                }
            }

            #if (DEBUG)
            #region debug_WriteThresholdRecordsToRDBMS_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("WriteThresholdRecordsToRDBMS completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

        }


        public void ProcessItemSenseMessages()
        {
            #if (DEBUG)
            #region debug_processItemSense_msg_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("ProcessItemSenseMessages started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            try
            {
                //First create extension tables if they don't exist
                CreateItemSenseRdbmsExtensionTables();

                //Next get last known threshold events and create temp table
                GetLatestEpcFromThresholdHist();

                //Now get all last know item events and create temp table
                GetLatestEpcFromItemEventHist();

                //Merge both tables
                MergeBothTempTables();
                
                //Call upsert epc master 
                UpsertEpcMasterFromTempTable();

                //Now upsert the count for each upc at each location
                UpsertUpcInventoryLocation();

                //Truncate the ItemSense Tables to size configured in app.config
                TruncateItemSenseHist();

               //Finally Truncate the Extension Tables to size configured
                TruncateExtensionTables();
            }
            catch (Exception ex)
            {
                string errMsg = "ProcessItemSenseMessages Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_processItemSense_msg_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("ProcessItemSenseMessages completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif
        }

        /// <summary>
        /// Truncates all summary table information in the Extension 
        /// Note*:  This does not truncate reference tables as well as epc_master by design
        /// </summary>
        private void TruncateExtensionTables()
        {
            #if (DEBUG)
            #region debug_TruncateExtensionTables_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("TruncateExtensionTables started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
            //Do Not Alter - These strings are modified via the app.cfg
            //Update History "updatedb_cmd"
            const string cmdText = @"IF EXISTS (SELECT * FROM sysobjects WHERE name= '{upc_inv_loc}' AND xtype = 'U') " +
                                   @"DELETE FROM {upc_inv_loc} WHERE DATEDIFF(Day, last_updt_time, GETDATE()) > interval '{ext_hist_interval} days';";

            string replText = cmdText.Replace("{ext_hist_interval}", ConfigurationManager.AppSettings["ItemSenseEventProcessingHistoryInterval(secs)"]);
            string cfgCmdText = replText.Replace("{upc_inv_loc}", ConfigurationManager.AppSettings["ItemSenseExtensionUpcInventoryLocationTableName"]);

            #endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);
                SqlCommand updatedb_cmd = new SqlCommand(cfgCmdText, conn);
                conn.Open();

                // Execute the truncation
                updatedb_cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "TruncateExtensionTables Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_TruncateExtensionTables_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("TruncateExtensionTables completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif
        }

        private static void TruncateItemSenseHist()
        {
            #if (DEBUG)
            #region debug_TruncateItemEventHist_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("TruncateItemSenseHist started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
            //Do Not Alter - These strings are modified via the app.cfg
            //Update History "updatedb_cmd"
            const string cmdText = @"DELETE FROM {is_raw_item_event_hist} WHERE DATEDIFF(second, obsv_time, getdate()) > interval '{is_hist_interval} seconds' ; " +
                                   @"DELETE FROM {is_threshold_hist} WHERE DATEDIFF(second, observation_time, getdate()) > interval '{is_hist_interval} seconds' ; ";

            string replText = cmdText.Replace("{is_raw_item_event_hist}", ConfigurationManager.AppSettings["ItemSenseRawItemEventHistTableName"]);
            string repl2Text = replText.Replace("{is_threshold_hist}", ConfigurationManager.AppSettings["ItemSenseThresholdHistTableName"]);
            string cfgCmdText = repl2Text.Replace("{is_hist_interval}", ConfigurationManager.AppSettings["ItemSenseEventProcessingHistoryInterval(secs)"]);

            #endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);
                SqlCommand updatedb_cmd = new SqlCommand(cfgCmdText, conn);
                conn.Open();

                // Execute the truncation
                updatedb_cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "TruncateItemSenseHist Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_TruncateItemEventHist_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("TruncateItemSenseHist completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif
        }

        private static void UpsertUpcInventoryLocation()
        {
            #if (DEBUG)
            #region debug_UpsertUpcInventoryLocation_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("UpsertUpcInventoryLocation started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
                        //Do Not Alter - These strings are modified via the app.cfg
                        //Update History "upsertdb_cmd"
                        const string postText = @"MERGE {upc_inv_loc} AS target USING (SELECT DISTINCT upc_nbr, floor, zone_name, facility, count(epc_nbr), GETDATE() " +
                            @"FROM {epc_master} GROUP BY upc_nbr, floor, zone_name, facility) AS source (upc_nbr, floor, zone_name, facility, " +
                            @"qty, last_updt_time) ON (target.upc_nbr = source.upc_nbr AND target.floor = source.floor AND target.zone_name = source.zone_name AND target.facility = source.facility) " +
                            @"WHEN MATCHED THEN UPDATE SET qty = source.qty, last_updt_time = source.last_updt_time WHEN NOT MATCHED THEN " +
                            @"INSERT (upc_nbr, floor, zone_name, facility, qty, last_updt_time) VALUES (source.upc_nbr, source.floor, source.zone_name, source.facility, " +
                            @"source.qty, source.last_updt_time); ";

                        string repText = postText.Replace("{upc_inv_loc}", ConfigurationManager.AppSettings["ItemSenseExtensionUpcInventoryLocationTableName"]);
                        string cfgCmdText = repText.Replace("{epc_master}", ConfigurationManager.AppSettings["ItemSenseExtensionEpcMasterTableName"]);
            #endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);
                SqlCommand upsertdb_cmd = new SqlCommand(cfgCmdText, conn);
                conn.Open();

                // Execute the insert / update to upc_inv_loc
                upsertdb_cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "UpsertUpcInventoryLocation Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_processItemSense_msg_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("UpsertUpcInventoryLocation completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif
        }

        private static void MergeBothTempTables()
        {
            #if (DEBUG)
            #region debug_MergeBothTempTables_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("MergeBothTempTables started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
            //Do Not Alter - These strings are modified via the app.cfg
            //Update History "updatedb_cmd"

            string updCmdText = @"IF EXISTS (SELECT * FROM sysobjects WHERE name='is_upc_tmp' AND xtype = 'U') DROP TABLE is_upc_tmp; " +
                    @"CREATE TABLE is_upc_tmp (epc_nbr varchar(128) NOT NULL,  last_obsv_time DateTime, tag_id varchar(128), " +
                    @"zone_name varchar(128), floor varchar(128), facility varchar(128), x_coord float, " +
                    @"y_coord float, upc_nbr varchar(24), last_updt_time DateTime, PRIMARY KEY(epc_nbr, last_obsv_time)); ";


            const string cmdText = @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{is_temp_upc}' AND xtype = 'U') CREATE TABLE " +
                    @"{is_temp_upc} (epc_nbr varchar(128) NOT NULL, last_obsv_time DateTime, tag_id varchar(128), " +
                    @"zone_name varchar(128), floor varchar(128), facility varchar(128), x_coord float, " +
                    @"y_coord float, upc_nbr varchar(24), last_updt_time DateTime, PRIMARY KEY(epc_nbr, last_obsv_time)); " +
                    @"MERGE is_upc_tmp AS target USING( " +
                    @"SELECT epc_nbr, last_obsv_time, tag_id, zone_name, floor, facility, x_coord, y_coord, upc_nbr, last_updt_time " +
                    @"FROM {is_temp_upc} t1 " +
                    @")AS source (epc_nbr, last_obsv_time, tag_id, zone_name, floor, facility, x_coord, y_coord, upc_nbr, last_updt_time) " +
                    @"ON (target.epc_nbr = source.epc_nbr) " +
                    @"WHEN MATCHED THEN UPDATE SET last_obsv_time = source.last_obsv_time, tag_id = source.tag_id, zone_name = source.zone_name, " +
                    @"floor = source.floor, facility = source.facility, x_coord = source.x_coord, y_coord = source.y_coord, " +
                    @"upc_nbr = source.upc_nbr, last_updt_time = source.last_updt_time " +
                    @"WHEN NOT MATCHED THEN INSERT (epc_nbr, last_obsv_time, tag_id, zone_name, floor, facility, x_coord, y_coord, " +
                    @"upc_nbr, last_updt_time) VALUES (source.epc_nbr, source.last_obsv_time, source.tag_id, source.zone_name, " +
                    @"source.floor, source.facility, source.x_coord, source.y_coord, source.upc_nbr, source.last_updt_time);";

            string cfgCmdText = cmdText.Replace("{is_temp_upc}", "is_upc_tmp_thresh");
            string postCmdText = cmdText.Replace("{is_temp_upc}", "is_upc_tmp_item");

#endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);

                SqlCommand updatedb_cmd = new SqlCommand(updCmdText, conn);
                SqlCommand mergedb_cmd = new SqlCommand(cfgCmdText, conn);
                SqlCommand postdb_cmd = new SqlCommand(postCmdText, conn);

                conn.Open();

                //First drop and create
                updatedb_cmd.ExecuteNonQuery();
                // Execute the merge Threshold
                mergedb_cmd.ExecuteNonQuery();
                // Finally merge the Item Events
                postdb_cmd.ExecuteNonQuery();

                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "MergeBothTempTables Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_MergeBothTempTables_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("MergeBothTempTables completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

        }
        private static void UpsertEpcMasterFromTempTable()
        {
            #if (DEBUG)
            #region debug_UpsertEpcMasterFromTempTable_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("UpsertEpcMasterFromTempTable started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
            //Do Not Alter - These strings are modified via the app.cfg
            //Update Epc Master History "upsertdb_cmd"
            const string postText = @"MERGE {epc_master} AS target USING (SELECT epc_nbr, last_obsv_time, tag_id, zone_name, floor, facility, x_coord, " +
                @"y_coord, last_updt_time, upc_nbr FROM is_upc_tmp) AS source (epc_nbr, last_obsv_time, tag_id, zone_name, floor, facility, x_coord, y_coord, " +
                @"last_updt_time, upc_nbr) ON (target.epc_nbr = source.epc_nbr AND target.last_obsv_time < source.last_obsv_time) " +
                @"WHEN MATCHED THEN UPDATE set last_obsv_time = source.last_obsv_time, " +
                @"tag_id = source.tag_id, zone_name = source.zone_name, floor = source.floor, facility = source.facility, x_coord = source.x_coord, " +
                @"y_coord = source.y_coord, last_updt_time = source.last_updt_time WHEN NOT MATCHED THEN INSERT (epc_nbr, last_obsv_time, " +
                @"tag_id, zone_name, floor, facility, x_coord, y_coord, last_updt_time, upc_nbr) VALUES (source.epc_nbr, source.last_obsv_time, source.tag_id, " +
                @"source.zone_name, source.floor, source.facility, source.x_coord, source.y_coord, source.last_updt_time, source.upc_nbr);";

            string cfgCmdText = postText.Replace("{epc_master}", ConfigurationManager.AppSettings["ItemSenseExtensionEpcMasterTableName"]);
#endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);
                SqlCommand upsertdb_cmd = new SqlCommand(cfgCmdText, conn);
                conn.Open();

                // Execute the insert / update to epc_master
                upsertdb_cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "UpsertEpcMasterFromTempTable Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_processItemSense_msg_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("UpsertEpcMasterFromTempTable completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif
        }

        private static void GetLatestEpcFromItemEventHist()
        {
            #if (DEBUG)
            #region debug_GetLatestEpcFromItemEventHist_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("GetLatestEpcFromItemEventHist started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
            //Do Not Alter - These strings are modified via the app.cfg
            const string cmdText =  @"IF EXISTS (SELECT * FROM sysobjects WHERE name='{is_raw_item_event_hist}' AND xtype = 'U') " +
                                    @"SELECT i1.epc_nbr, i1.obsv_time, i1.tag_id, i1.to_zone, i1.to_floor, i1.to_fac, " +
                                    @"i1.to_x, i1.to_y FROM {is_raw_item_event_hist} i1 " +
                                    @"WHERE i1.obsv_time = (SELECT MAX(obsv_time) FROM {is_raw_item_event_hist} i2 WHERE i1.epc_nbr = i2.epc_nbr) " +
                                    @"GROUP BY epc_nbr, obsv_time, tag_id, to_zone, to_floor, to_fac, to_x, to_y; ";

            string cfgCmdText = cmdText.Replace("{is_raw_item_event_hist}", ConfigurationManager.AppSettings["ItemSenseRawItemEventHistTableName"]);

            string updCmdText = @"IF EXISTS (SELECT * FROM sysobjects WHERE name='is_upc_tmp_item' AND xtype = 'U') DROP TABLE is_upc_tmp_item; " +
                @"CREATE TABLE is_upc_tmp_item (epc_nbr varchar(128) NOT NULL,  last_obsv_time DateTime, tag_id varchar(128), " +
                @"zone_name varchar(128), floor varchar(128), facility varchar(128), x_coord float, " +
                @"y_coord float, upc_nbr varchar(24), last_updt_time DateTime,  PRIMARY KEY(epc_nbr)); ";


#endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);
                SqlCommand selectdb_cmd = new SqlCommand(cfgCmdText, conn);
                conn.Open();

                // Execute the query and obtain a result set
                SqlDataReader dr = selectdb_cmd.ExecuteReader();

                // Pull UPC from SGTIN reads only and Output rows to array list for insertion into UPC temp table
                while (dr.Read())
                {
                    if (Sgtin96.IsValidSGTIN(dr[0].ToString()))
                    {
                        Sgtin96 gtin = Sgtin96.FromString(dr[0].ToString());
                        EpcMasterRec rec = new EpcMasterRec(dr[0].ToString(), Convert.ToDateTime(dr[1].ToString()), dr[2].ToString(),
                            dr[3].ToString(), dr[4].ToString(), dr[5].ToString(), Convert.ToDouble(dr[6]), Convert.ToDouble(dr[7]),
                            gtin.ToUpc(), DateTime.Now);
                        mastEventRecords.Add(rec);
                    }
                    else
                    {
                        //Do proprietary encoding filter to upc_nbr
                        EpcMasterRec rec = new EpcMasterRec(dr[0].ToString(), Convert.ToDateTime(dr[1].ToString()), dr[2].ToString(),
                            dr[3].ToString(), dr[4].ToString(), dr[5].ToString(), Convert.ToDouble(dr[6]), Convert.ToDouble(dr[7]),
                            GetCustomUpc(dr[0].ToString()), Convert.ToDateTime(dr[1].ToString()));
                        mastEventRecords.Add(rec);
                    }
                }
                dr.Close();

                //Copy the upc records into a data table so we may create a data reader for use with bulk copy
                dtEvents.Clear();
                foreach (EpcMasterRec rec in mastEventRecords)
                    dtEvents.Rows.Add(rec.Epc, rec.ObservationTime, rec.TagId, rec.ZoneName, rec.Floor, rec.Facility, rec.Xcoord, rec.Ycoord, rec.Upc, rec.LastUpdateTime);

                //Drop and create temp table
                SqlCommand update_cmd = new SqlCommand(updCmdText, conn);
                update_cmd.ExecuteNonQuery();

                DataTableReader reader = dtEvents.CreateDataReader();

                //Bulk insert into the temp upc table events just read
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn))
                {
                    bulkCopy.DestinationTableName = "is_upc_tmp_item";
                    bulkCopy.WriteToServer(reader);
                }

                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "GetLatestEpcFromItemEventHist Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_GetLatestEpcFromItemEventHist_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("DoItemEventRecordsETLM completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

        }

        private void GetLatestEpcFromThresholdHist()
        {
            #if (DEBUG)
            #region debug_GetLatestEpcFromThresholdHist_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("GetLatestEpcFromThresholdHist started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
            //Do Not Alter - These strings are modified via the app.cfg
            const string cmdText =  @"IF EXISTS (SELECT * FROM sysobjects WHERE name='{is_threshold_hist}' AND xtype = 'U') " +
                                    @"SELECT i1.epc_nbr, i1.observation_time, i1.to_zone FROM {is_threshold_hist} i1 " +
                                    @"WHERE i1.observation_time = (SELECT MAX(observation_time) FROM {is_threshold_hist} i2 WHERE i1.epc_nbr = i2.epc_nbr) " +
                                    @"GROUP BY epc_nbr, observation_time, to_zone; ";

            string cfgCmdText = cmdText.Replace("{is_threshold_hist}", ConfigurationManager.AppSettings["ItemSenseThresholdHistTableName"]);

            string updCmdText = @"IF EXISTS (SELECT * FROM sysobjects WHERE name='is_upc_tmp_thresh' AND xtype = 'U') DROP TABLE is_upc_tmp_thresh; " +
                @"CREATE TABLE is_upc_tmp_thresh (epc_nbr varchar(128) NOT NULL,  last_obsv_time DateTime, tag_id varchar(128), " +
                @"zone_name varchar(128), floor varchar(128), facility varchar(128), x_coord float, " +
                @"y_coord float, upc_nbr varchar(24), last_updt_time DateTime, PRIMARY KEY(epc_nbr)); ";

#endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);
                SqlCommand selectdb_cmd = new SqlCommand(cfgCmdText, conn);
                conn.Open();

                // Execute the query and obtain a result set
                SqlDataReader dr = selectdb_cmd.ExecuteReader();

                // Pull UPC from SGTIN reads only and Output rows to array list for insertion into UPC temp table
                while (dr.Read())
                {
                    if (Convert.ToBoolean(ConfigurationManager.AppSettings["Sgtin96Encoded"]))
                    {
                        if (Sgtin96.IsValidSGTIN(dr[0].ToString()))
                        {
                            Sgtin96 gtin = Sgtin96.FromString(dr[0].ToString());
                            EpcMasterRec rec = new EpcMasterRec(dr[0].ToString(), Convert.ToDateTime(dr[1].ToString()), dr[2].ToString(),
                                string.Empty, string.Empty, string.Empty, 0, 0, gtin.ToUpc(), DateTime.Now);
                            mastEventRecords.Add(rec);
                        }
                    }
                    else
                    {
                        //Do proprietary encoding filter to upc_nbr
                        EpcMasterRec rec = new EpcMasterRec(dr[0].ToString(), Convert.ToDateTime(dr[1].ToString()), dr[2].ToString(),
                            dr[3].ToString(), dr[4].ToString(), dr[5].ToString(), Convert.ToDouble(dr[6]), Convert.ToDouble(dr[7]),
                            GetCustomUpc(dr[0].ToString()), Convert.ToDateTime(dr[1].ToString()));
                        mastEventRecords.Add(rec);
                    }
                }
                dr.Close();

                //Copy the upc records into a data table so we may create a data reader for use with bulk copy
                dtEvents.Clear();
                foreach (EpcMasterRec rec in mastEventRecords)
                    dtEvents.Rows.Add(rec.Epc, rec.ObservationTime, rec.TagId, rec.ZoneName, rec.Floor, rec.Facility, rec.Xcoord, rec.Ycoord, rec.Upc, rec.LastUpdateTime);

                //Drop and create temp table
                SqlCommand update_cmd = new SqlCommand(updCmdText, conn);
                update_cmd.ExecuteNonQuery();

                DataTableReader reader = dtEvents.CreateDataReader();

                //Bulk insert into the temp upc table events just read
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn))
                {
                    bulkCopy.DestinationTableName = "is_upc_tmp_thresh";
                    bulkCopy.WriteToServer(reader);
                }
                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "GetLatestEpcFromThresholdHist Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_GetLatestEpcFromThresholdHist_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("GetLatestEpcFromThresholdHist completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif
        }

        private void CreateItemSenseRdbmsExtensionTables()
        {
            #if (DEBUG)
            #region debug_CreateItemSenseRdbmsExtensionTables_kpi
                        DateTime blockTmSt = System.DateTime.Now;
                        iLog.WriteEntry("CreateItemSenseRdbmsExtensionTables started: " + blockTmSt.ToLongTimeString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif

            #region Postgresql DDL
            //Create "createdb_cmd"
            const string cmdText = @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{epc_master}' AND xtype = 'U') CREATE TABLE " +
                @"{epc_master} (epc_nbr varchar(128) NOT NULL UNIQUE, last_obsv_time DateTime, tag_id varchar(128), zone_name varchar(128), " +
                @"floor varchar(128), facility varchar(128), x_coord float, y_coord float, last_updt_time DateTime,  " +
                @"upc_nbr varchar(24), PRIMARY KEY (epc_nbr)); " +
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{upc_inv_loc}' AND xtype = 'U') CREATE TABLE " +
                @"{upc_inv_loc} (upc_nbr varchar(24) NOT NULL, floor varchar(128), zone_name varchar(128), facility varchar(128), qty int, " +
                @"last_updt_time DateTime, PRIMARY KEY (upc_nbr, floor, zone_name, facility)); " +
                @"IF NOT EXISTS (SELECT * FROM sysindexes WHERE name='UK_{upc_inv_loc}_upc_floor_zone_fac') " +
                @"CREATE UNIQUE INDEX  UK_{upc_inv_loc}_upc_floor_zone_fac ON {upc_inv_loc} (upc_nbr, floor, zone_name, facility); " +
                @"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='item' AND xtype = 'U') CREATE TABLE " +
                @"item (upc_nbr varchar(24), dept_nbr int, retail_price float, item_cost float, item_nbr int, avg_rate_of_sale float, " +
                @"item_desc varchar(128), mfg_name varchar(128), shelf_qty int, on_hand int, PRIMARY KEY (upc_nbr)); ";

               string rplTxt = cmdText.Replace("{epc_master}", ConfigurationManager.AppSettings["ItemSenseExtensionEpcMasterTableName"]);
               string cfgCmdText = rplTxt.Replace("{upc_inv_loc}", ConfigurationManager.AppSettings["ItemSenseExtensionUpcInventoryLocationTableName"]);

            #endregion

            try
            {
                string connStr = ConfigurationManager.AppSettings["DbConnectionString"];
                SqlConnection conn = new SqlConnection(connStr);
                SqlCommand createdb_cmd = new SqlCommand(cfgCmdText, conn);
                conn.Open();
                //Create history table if necessary, drop and recreate the temporary threshold table
                createdb_cmd.ExecuteNonQuery();
                conn.Close();
            }
            catch (Exception ex)
            {
                string errMsg = "CreateItemSenseRdbmsExtensionTables Exception: " + ex.Message + "(" + ex.GetType() + ")";
                if (null != ex.InnerException)
                    errMsg += Environment.NewLine + ex.InnerException.Message;
                iLog.WriteEntry(errMsg, EventLogEntryType.Error, eventId); eventId++;
            }

            #if (DEBUG)
            #region debug_CreateItemSenseRdbmsExtensionTables_kpi
                        DateTime procTmEnd = DateTime.Now;
                        TimeSpan procTmSpan = procTmEnd.Subtract(blockTmSt);
                        iLog.WriteEntry("CreateItemSenseRdbmsExtensionTables completed(ms): " + procTmSpan.Milliseconds.ToString(), EventLogEntryType.Information, eventId); eventId++;
            #endregion
            #endif
        }
    }
}
