﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
namespace Fireasy.Data.Entity
{
    public class EntityContextOptions
    {
        /// <summary>
        /// 初始化 <see cref="EntityContextOptions"/> 类的新实例。
        /// </summary>
        public EntityContextOptions()
        {
        }

        /// <summary>
        /// 初始化 <see cref="EntityContextOptions"/> 类的新实例。
        /// </summary>
        /// <param name="instanceName">实例名称。</param>
        public EntityContextOptions(string instanceName)
        {
            ConfigName = instanceName;
        }

        /// <summary>
        /// 获取或设置是否自动创建数据表。默认为 false。
        /// </summary>
        public bool AutoCreateTables { get; set; }

        /// <summary>
        /// 获取或设置是否通知持久化事件。默认为 false。
        /// </summary>
        public bool NotifyEvents { get; set; } = false;

        /// <summary>
        /// 获取或设置是否验证实体属性。默认为 true。
        /// </summary>
        public bool ValidateEntity { get; set; } = true;

        /// <summary>
        /// 获取实例名称。
        /// </summary>
        public string ConfigName { get; private set; }
    }
}
