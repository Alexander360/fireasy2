﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using Fireasy.Data.Extensions;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Transactions;

namespace Fireasy.Data
{
    /// <summary>
    /// 用于管理处于 <see cref="TransactionScope"/> 中的数据库链接。无法继承此类。
    /// </summary>
    public sealed class TransactionScopeConnections
    {
        private static readonly ConcurrentDictionary<Transaction, ConcurrentDictionary<string, DbConnection>> transConns =
            new ConcurrentDictionary<Transaction, ConcurrentDictionary<string, DbConnection>>();


        /// <summary>
        /// 从分布式事务环境中获取数据库链接对象。
        /// </summary>
        /// <param name="database">数据库对象。</param>
        /// <returns>如果未启用分布式事务，则为 null，否则为对应 <see cref="IDatabase"/> 的数据库链接对象。</returns>
        public static DbConnection GetConnection(IDatabase database)
        {
            var curTrans = Transaction.Current;

            if (curTrans == null)
            {
                return null;
            }

            if (!transConns.TryGetValue(curTrans, out ConcurrentDictionary<string, DbConnection> connDictionary))
            {
                connDictionary = new ConcurrentDictionary<string, DbConnection>();
                transConns.TryAdd(curTrans, connDictionary);

                Debug.WriteLine("Transaction registered.");
                curTrans.TransactionCompleted += OnTransactionCompleted;
            }

            var connStr = database.ConnectionString.ToString();
            if (!connDictionary.TryGetValue(connStr, out DbConnection connection))
            {
                connection = database.Provider.DbProviderFactory.CreateConnection();
                if (connection != null)
                {
                    connection.ConnectionString = connStr;
                    connection.TryOpen();
                    connection.EnlistTransaction(curTrans);
                    connDictionary.TryAdd(connStr, connection);

                    Debug.WriteLine("DbConnection of '" + connStr + "' registered.");
                }
            }
            else
            {
                Debug.WriteLine("DbConnection get from cache.");
            }

            return connection;
        }

        /// <summary>
        /// 事务完成后，关闭数据库链接。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void OnTransactionCompleted(object sender, TransactionEventArgs e)
        {
            if (!transConns.TryGetValue(e.Transaction, out ConcurrentDictionary<string, DbConnection> connDictionary))
            {
                return;
            }

            Debug.WriteLine("Transaction completed.");
            foreach (var connection in connDictionary.Values)
            {
                connection.TryClose();
                connection.Dispose();
            }

            transConns.TryRemove(e.Transaction, out connDictionary);
        }
    }
}
