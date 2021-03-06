﻿namespace ServiceControl.Config.Framework.Rx
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using Caliburn.Micro;
    using EnumerableExtensions = EnumerableExtensions;

    public partial class RxConductor<T>
    {
        public class Collection
        {
            public class OneActive : RxConductorBaseWithActiveItem<T>
            {
                public OneActive()
                {
                    items.CollectionChanged += (s, e) =>
                    {
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Add:
                                EnumerableExtensions.Apply(e.NewItems.OfType<IChild>(), x => x.Parent = this);
                                break;

                            case NotifyCollectionChangedAction.Remove:
                                EnumerableExtensions.Apply(e.OldItems.OfType<IChild>(), x => x.Parent = null);
                                break;

                            case NotifyCollectionChangedAction.Replace:
                                EnumerableExtensions.Apply(e.NewItems.OfType<IChild>(), x => x.Parent = this);
                                EnumerableExtensions.Apply(e.OldItems.OfType<IChild>(), x => x.Parent = null);
                                break;

                            case NotifyCollectionChangedAction.Reset:
                                EnumerableExtensions.Apply(items.OfType<IChild>(), x => x.Parent = this);
                                break;
                        }
                    };
                }

                public IObservableCollection<T> Items => items;

                public override IEnumerable<T> GetChildren()
                {
                    return items;
                }

                public override void ActivateItem(T item)
                {
                    if (item != null && item.Equals(ActiveItem))
                    {
                        if (IsActive)
                        {
                            ScreenExtensions.TryActivate(item);
                            OnActivationProcessed(item, true);
                        }

                        return;
                    }

                    ChangeActiveItem(item, false);
                }

                public override void DeactivateItem(T item, bool close)
                {
                    if (item == null)
                    {
                        return;
                    }

                    if (!close)
                    {
                        ScreenExtensions.TryDeactivate(item, false);
                    }
                    else
                    {
                        CloseStrategy.Execute(new[] {item}, (canClose, closable) =>
                        {
                            if (canClose)
                            {
                                CloseItemCore(item);
                            }
                        });
                    }
                }

                void CloseItemCore(T item)
                {
                    if (item.Equals(ActiveItem))
                    {
                        var index = items.IndexOf(item);
                        var next = DetermineNextItemToActivate(items, index);

                        ChangeActiveItem(next, true);
                    }
                    else
                    {
                        ScreenExtensions.TryDeactivate(item, true);
                    }

                    items.Remove(item);
                }

                protected virtual T DetermineNextItemToActivate(IList<T> list, int lastIndex)
                {
                    var toRemoveAt = lastIndex - 1;

                    if (toRemoveAt == -1 && list.Count > 1)
                    {
                        return list[1];
                    }

                    if (toRemoveAt > -1 && toRemoveAt < list.Count - 1)
                    {
                        return list[toRemoveAt];
                    }

                    return default;
                }

                public override void CanClose(Action<bool> callback)
                {
                    CloseStrategy.Execute(items.ToList(), (canClose, closable) =>
                    {
                        if (!canClose && closable.Any())
                        {
                            if (closable.Contains(ActiveItem))
                            {
                                var list = items.ToList();
                                var next = ActiveItem;
                                do
                                {
                                    var previous = next;
                                    next = DetermineNextItemToActivate(list, list.IndexOf(previous));
                                    list.Remove(previous);
                                } while (closable.Contains(next));

                                var previousActive = ActiveItem;
                                ChangeActiveItem(next, true);
                                items.Remove(previousActive);

                                var stillToClose = closable.ToList();
                                stillToClose.Remove(previousActive);
                                closable = stillToClose;
                            }

                            EnumerableExtensions.Apply(closable.OfType<IDeactivate>(), x => x.Deactivate(true));
                            items.RemoveRange(closable);
                        }

                        callback(canClose);
                    });
                }

                protected override void OnActivate()
                {
                    ScreenExtensions.TryActivate(ActiveItem);
                }

                protected override void OnDeactivate(bool close)
                {
                    if (close)
                    {
                        EnumerableExtensions.Apply(items.OfType<IDeactivate>(), x => x.Deactivate(true));
                        items.Clear();
                    }
                    else
                    {
                        ScreenExtensions.TryDeactivate(ActiveItem, false);
                    }
                }

                protected override T EnsureItem(T newItem)
                {
                    if (newItem == null)
                    {
                        newItem = DetermineNextItemToActivate(items, ActiveItem != null ? items.IndexOf(ActiveItem) : 0);
                    }
                    else
                    {
                        var index = items.IndexOf(newItem);

                        if (index == -1)
                        {
                            items.Add(newItem);
                        }
                        else
                        {
                            newItem = items[index];
                        }
                    }

                    return base.EnsureItem(newItem);
                }

                readonly BindableCollection<T> items = new BindableCollection<T>();
            }
        }
    }
}