﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using Fireasy.Common.Extensions;
using Fireasy.Data.Batcher;
using Fireasy.Data.Entity.Linq;
using Fireasy.Data.Entity.Metadata;
using Fireasy.Data.Entity.Properties;
using Fireasy.Data.Entity.Subscribes;
using Fireasy.Data.Entity.Validation;
using Fireasy.Data.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Fireasy.Data.Entity
{
    /// <summary>
    /// 缺省的仓储服务实现，使用 Linq to SQL。
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public sealed class DefaultRepositoryProvider<TEntity> : IRepositoryProvider<TEntity> where TEntity : IEntity
    {
        private InternalContext context;

        /// <summary>
        /// 初始化 <see cref="DefaultRepositoryProvider"/> 类的新实例。
        /// </summary>
        /// <param name="context"></param>
        public DefaultRepositoryProvider(InternalContext context)
        {
            this.context = context;
            var entityQueryProvider = new EntityQueryProvider(context);
            context.As<IEntityPersistentInstanceContainer>(s => entityQueryProvider.InitializeInstanceName(s.InstanceName));
            QueryProvider = new QueryProvider(entityQueryProvider);
            Queryable = new QuerySet<TEntity>(QueryProvider, null);
        }

        /// <summary>
        /// 获取 <see cref="IQueryable"/> 对象。
        /// </summary>
        public IQueryable Queryable { get; private set; }

        /// <summary>
        /// 获取 <see cref="IQueryProvider"/> 对象。
        /// </summary>
        public IQueryProvider QueryProvider { get; private set; }

        /// <summary>
        /// 将一个新的实体对象插入到库。
        /// </summary>
        /// <param name="entity">要创建的实体对象。</param>
        /// <returns>影响的实体数。</returns>
        public int Insert(TEntity entity)
        {
            EntityPersistentSubscribePublisher.OnBeforeCreate(entity);
            ValidationUnity.Validate(entity);

            var trans = CheckRelationHasModified(entity);
            if (trans)
            {
                context.Database.BeginTransaction();
            }

            int result = 0;

            try
            {
                if ((result = Queryable.CreateEntity(entity)) > 0)
                {
                    entity.As<IEntityPersistentEnvironment>(s => s.Environment = context.Environment);
                    entity.As<IEntityPersistentInstanceContainer>(s => s.InstanceName = context.InstanceName);

                    HandleRelationProperties(entity);
                    EntityPersistentSubscribePublisher.OnAfterCreate(entity);
                }

                if (trans)
                {
                    context.Database.CommitTransaction();
                }
            }
            catch (Exception exp)
            {
                if (trans)
                {
                    context.Database.RollbackTransaction();
                }

                throw exp;
            }

            return result;
        }

        /// <summary>
        /// 更新一个实体对象。
        /// </summary>
        /// <param name="entity">要更新的实体对象。</param>
        /// <returns>影响的实体数。</returns>
        public int Update(TEntity entity)
        {
            EntityPersistentSubscribePublisher.OnBeforeUpdate(entity);
            ValidationUnity.Validate(entity);

            var trans = CheckRelationHasModified(entity);
            if (trans)
            {
                context.Database.BeginTransaction();
            }

            int result = 0;

            try
            {
                if ((result = Queryable.UpdateEntity(entity)) > 0)
                {
                    EntityPersistentSubscribePublisher.OnAfterUpdate(entity);
                }

                HandleRelationProperties(entity);

                if (trans)
                {
                    context.Database.CommitTransaction();
                }
            }
            catch (Exception exp)
            {
                if (trans)
                {
                    context.Database.RollbackTransaction();
                }

                throw exp;
            }

            return result;
        }

        /// <summary>
        /// 批量将一组实体对象插入到库中。
        /// </summary>
        /// <param name="entities">一组要插入实体对象。</param>
        /// <param name="batchSize">每一个批次插入的实体数量。默认为 1000。</param>
        /// <param name="completePercentage">已完成百分比的通知方法。</param>
        public void BatchInsert(IEnumerable<TEntity> entities, int batchSize = 1000, Action<int> completePercentage = null)
        {
            var batcher = context.Database.Provider.GetService<IBatcherProvider>();
            if (batcher == null)
            {
                throw new EntityPersistentException(SR.GetString(SRKind.NotSupportBatcher), null);
            }

            var syntax = context.Database.Provider.GetService<ISyntaxProvider>();
            var rootType = typeof(TEntity).GetRootType();
            var tableName = string.Empty;

            entities.ForEach(s => EntityPersistentSubscribePublisher.OnBeforeCreate(s));

            if (context.Environment != null)
            {
                tableName = DbUtility.FormatByQuote(syntax, context.Environment.GetVariableTableName(rootType));
            }
            else
            {
                var metadata = EntityMetadataUnity.GetEntityMetadata(rootType);
                tableName = DbUtility.FormatByQuote(syntax, metadata.TableName);
            }

            batcher.Insert(context.Database, entities, tableName, batchSize, completePercentage);
        }

        /// <summary>
        /// 将指定的实体对象从库中删除。
        /// </summary>
        /// <param name="entity">要移除的实体对象。</param>
        /// <param name="logicalDelete">是否为逻辑删除。</param>
        /// <returns>影响的实体数。</returns>
        public int Delete(TEntity entity, bool logicalDelete = true)
        {
            EntityPersistentSubscribePublisher.OnBeforeRemove(entity);

            int result;
            if ((result = Queryable.RemoveEntity(entity, logicalDelete)) > 0)
            {
                EntityPersistentSubscribePublisher.OnAfterRemove(entity);
            }

            return result;
        }

        /// <summary>
        /// 根据主键值将对象从库中删除。
        /// </summary>
        /// <param name="primaryValues">一组主键值。</param>
        /// <returns></returns>
        public int Delete(params PropertyValue[] primaryValues)
        {
            return Queryable.RemoveByPrimary(primaryValues, true);
        }

        /// <summary>
        /// 根据主键值将对象从库中删除。
        /// </summary>
        /// <param name="primaryValues">一组主键值。</param>
        /// <param name="logicalDelete">是否为逻辑删除。</param>
        /// <returns></returns>
        public int Delete(PropertyValue[] primaryValues, bool logicalDelete = true)
        {
            if (primaryValues.Any(s => PropertyValue.IsEmpty(s)))
            {
                return 0;
            }

            return Queryable.RemoveByPrimary(primaryValues, logicalDelete);
        }

        /// <summary>
        /// 根据主键值将对象从库中删除。
        /// </summary>
        /// <param name="primaryValues">一组主键值。</param>
        /// <param name="logicalDelete">是否为逻辑删除。</param>
        /// <returns></returns>
        public int Delete(object[] primaryValues, bool logicalDelete = true)
        {
            return Queryable.RemoveByPrimary(primaryValues, logicalDelete);
        }

        /// <summary>
        /// 通过一组主键值返回一个实体对象。
        /// </summary>
        /// <param name="primaryValues">一组主键值。</param>
        /// <returns>影响的实体数。</returns>
        public TEntity Get(params PropertyValue[] primaryValues)
        {
            if (primaryValues.Any(s => PropertyValue.IsEmpty(s)))
            {
                return default(TEntity);
            }

            return Queryable.GetByPrimary<TEntity, PropertyValue>(primaryValues);
        }

        /// <summary>
        /// 将满足条件的一组对象从库中移除。
        /// </summary>
        /// <param name="predicate">用于测试每个元素是否满足条件的函数。</param>
        /// <param name="logicalDelete">是否为逻辑删除</param>
        /// <returns>影响的实体数。</returns>
        public int Delete(Expression<Func<TEntity, bool>> predicate, bool logicalDelete = true)
        {
            return Queryable.RemoveWhere(predicate, logicalDelete);
        }

        /// <summary>
        /// 使用一个参照的实体对象更新满足条件的一序列对象。
        /// </summary>
        /// <param name="entity">更新的参考对象。</param>
        /// <param name="predicate">用于测试每个元素是否满足条件的函数。</param>
        /// <returns>影响的实体数。</returns>
        public int Update(TEntity entity, Expression<Func<TEntity, bool>> predicate)
        {
            return Queryable.UpdateWhere(entity, predicate);
        }

        /// <summary>
        /// 使用一个累加器更新满足条件的一序列对象。
        /// </summary>
        /// <param name="calculator">一个计算器表达式。</param>
        /// <param name="predicate">用于测试每个元素是否满足条件的函数。</param>
        /// <returns>影响的实体数。</returns>
        public int Update(Expression<Func<TEntity, TEntity>> calculator, Expression<Func<TEntity, bool>> predicate)
        {
            return Queryable.UpdateWhere(calculator, predicate);
        }

        /// <summary>
        /// 对实体集合进行批量操作。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instances"></param>
        /// <param name="fnOperation"></param>
        /// <returns>影响的实体数。</returns>
        public int Batch(IEnumerable<TEntity> instances, Expression<Func<IRepository<TEntity>, TEntity, int>> fnOperation)
        {
            return Queryable.BatchOperate(instances.Cast<IEntity>(), fnOperation);
        }

        /// <summary>
        /// 检查有没有关联属性被修改.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private bool CheckRelationHasModified(IEntity entity)
        {
            //判断实体类型有是不是编译的代理类型，如果不是，取非null的属性，否则使用IsModified判断
            var isNotCompiled = entity.GetType().IsNotCompiled();
            return PropertyUnity.GetRelatedProperties(entity.GetType()).Any(s => isNotCompiled ? !PropertyValue.IsEmpty(entity.GetValue(s)) : entity.IsModified(s.Name));
        }

        /// <summary>
        /// 检查实体的关联属性。
        /// </summary>
        /// <param name="entity"></param>
        private void HandleRelationProperties(IEntity entity)
        {
            //判断实体类型有是不是编译的代理类型，如果不是，取非null的属性，否则使用IsModified判断
            var isNotCompiled = entity.GetType().IsNotCompiled();
            var properties = PropertyUnity.GetRelatedProperties(entity.GetType()).Where(m => isNotCompiled ?
                    !PropertyValue.IsEmpty(entity.GetValue(m)) :
                    entity.IsModified(m.Name));

            HandleRelationProperties(entity, properties);
        }

        /// <summary>
        /// 处理实体的关联的属性。
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="properties"></param>
        private void HandleRelationProperties(IEntity entity, IEnumerable<IProperty> properties)
        {
            foreach (RelationProperty property in properties)
            {
                var queryable = (IQueryable)context.GetDbSet(property.RelationType);

                switch (property.RelationPropertyType)
                {
                    case RelationPropertyType.Entity:
                        var refEntity = (IEntity)entity.GetValue(property).GetValue();
                        switch (refEntity.EntityState)
                        {
                            case EntityState.Modified:
                                queryable.UpdateEntity(refEntity);
                                refEntity.SetState(EntityState.Unchanged);
                                break;
                        }

                        HandleRelationProperties(refEntity);
                        break;
                    case RelationPropertyType.EntitySet:
                        var value = entity.GetValue(property);
                        if (PropertyValue.IsEmpty(value))
                        {
                            value = entity.GetOldValue(property);
                        }

                        if (!PropertyValue.IsEmpty(value))
                        {
                            HandleRelationEntitySet(queryable, entity, value.GetValue() as IEntitySet, property);
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// 处理关联的实体集合。
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="entity"></param>
        /// <param name="entitySet"></param>
        /// <param name="property"></param>
        private void HandleRelationEntitySet(IQueryable queryable, IEntity entity, IEntitySet entitySet, IProperty property)
        {
            var added = entitySet.GetAttachedList();
            var modified = entitySet.GetModifiedList();
            var deleted = entitySet.GetDetachedList();

            //处理删除的
            if (deleted.Count() > 0)
            {
                queryable.BatchOperate(deleted, queryable.CreateDeleteExpression(true));
            }

            //处理修改的
            if (modified.Count() > 0)
            {
                if (entitySet.AllowBatchUpdate)
                {
                    queryable.BatchOperate(modified, queryable.CreateUpdateExpression());
                }
                else
                {
                    foreach (var e in modified)
                    {
                        queryable.UpdateEntity(e);
                        e.SetState(EntityState.Unchanged);
                        HandleRelationProperties(e);
                    }
                }
            }

            //处理新增的
            if (added.Count() > 0)
            {
                var relation = RelationshipUnity.GetRelationship(property);
                added.ForEach(e =>
                    {
                        foreach (var key in relation.Keys)
                        {
                            var value = entity.GetValue(key.ThisProperty);
                            e.SetValue(key.OtherProperty, value);
                        }
                    });

                if (entitySet.AllowBatchInsert)
                {
                    queryable.BatchOperate(added, queryable.CreateInsertExpression());
                }
                else
                {
                    foreach (var e in added)
                    {
                        queryable.CreateEntity(e);
                        e.SetState(EntityState.Unchanged);
                        HandleRelationProperties(e);
                    }
                }
            }

            entitySet.Reset();
        }
    }
}
