﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using Fireasy.Common.Configuration;
using Fireasy.Common.Extensions;
using System.Collections.Generic;
#if NETSTANDARD
using CSRedis;
using System.Text;
#else
using StackExchange.Redis;
#endif
using System;

namespace Fireasy.Redis
{
    public class RedisComponent : IConfigurationSettingHostService
    {
#if NETSTANDARD
        private Lazy<CSRedisClient> connectionLazy;
#else
        private Lazy<ConnectionMultiplexer> connectionLazy;

        protected ConfigurationOptions Options { get; private set; }
#endif

        protected RedisConfigurationSetting Setting { get; private set; }

        /// <summary>
        /// 序列化对象。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual T Deserialize<T>(string value)
        {
            var serializer = CreateSerializer();
            return serializer.Deserialize<T>(value);
        }

        /// <summary>
        /// 序列化对象。
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual object Deserialize(Type type, string value)
        {
            var serializer = CreateSerializer();
            return serializer.Deserialize(type, value);
        }

        /// <summary>
        /// 反序列化。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual string Serialize<T>(T value)
        {
            var serializer = CreateSerializer();
            return serializer.Serialize(value);
        }

        private RedisSerializer CreateSerializer()
        {
            if (Setting.SerializerType != null)
            {
                var serializer = Setting.SerializerType.New<RedisSerializer>();
                if (serializer != null)
                {
                    return serializer;
                }
            }

            return new RedisSerializer();
        }

#if NETSTANDARD
        protected CSRedisClient GetConnection()
        {
            return connectionLazy.Value;
        }

#else
        protected ConnectionMultiplexer GetConnection()
        {
            return connectionLazy.Value;
        }
#endif

        void IConfigurationSettingHostService.Attach(IConfigurationSettingItem setting)
        {
            this.Setting = (RedisConfigurationSetting)setting;

#if NETSTANDARD
            var connectionStrs = new List<string>();
            if (!string.IsNullOrEmpty(this.Setting.ConnectionString))
            {
                connectionStrs.Add(this.Setting.ConnectionString);
            }
            else
            {

                foreach (var host in this.Setting.Hosts)
                {
                    var connStr = new StringBuilder($"{host.Server}");

                    #region connection build
                    if (host.Port != 0)
                    {
                        connStr.Append($":{host.Port}");
                    }

                    if (!string.IsNullOrEmpty(this.Setting.Password))
                    {
                        connStr.Append($",password={this.Setting.Password}");
                    }

                    if (this.Setting.DefaultDb != 0)
                    {
                        connStr.Append($",defaultDatabase={this.Setting.DefaultDb}");
                    }

                    connStr.Append(",allowAdmin=true");
                    #endregion

                    connectionStrs.Add(connStr.ToString());
                }
            }

            connectionLazy = new Lazy<CSRedisClient>(() => new CSRedisClient(null, connectionStrs.ToArray()));
#else
            if (!string.IsNullOrEmpty(this.Setting.ConnectionString))
            {
                Options = ConfigurationOptions.Parse(this.Setting.ConnectionString);
            }
            else
            {
                Options = new ConfigurationOptions
                {
                    DefaultDatabase = this.Setting.DefaultDb,
                    Password = this.Setting.Password,
                    AllowAdmin = true,
                    Proxy = this.Setting.Twemproxy ? Proxy.Twemproxy : Proxy.None,
                    AbortOnConnectFail = false
                };

                if (this.Setting.ConnectTimeout != null)
                {
                    Options.ConnectTimeout = (int)this.Setting.ConnectTimeout;
                }

                foreach (var host in this.Setting.Hosts)
                {
                    if (host.Port == 0)
                    {
                        Options.EndPoints.Add(host.Server);
                    }
                    else
                    {
                        Options.EndPoints.Add(host.Server, host.Port);
                    }
                }
            }

            connectionLazy = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(Options));
#endif
        }

        IConfigurationSettingItem IConfigurationSettingHostService.GetSetting()
        {
            return Setting;
        }
    }
}
