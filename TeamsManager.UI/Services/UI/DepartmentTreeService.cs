using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels.Departments;

namespace TeamsManager.UI.Services.UI
{
    /// <summary>
    /// Serwis do budowania struktury drzewa działów
    /// </summary>
    public class DepartmentTreeService
    {
        /// <summary>
        /// Buduje drzewo działów z płaskiej listy
        /// </summary>
        public List<DepartmentTreeItemViewModel> BuildDepartmentTree(IEnumerable<Department> departments)
        {
            var departmentDict = departments.ToDictionary(d => d.Id);
            var rootItems = new List<DepartmentTreeItemViewModel>();
            var itemDict = new Dictionary<string, DepartmentTreeItemViewModel>();

            // Pierwszy przebieg - tworzenie wszystkich ViewModels
            foreach (var department in departments)
            {
                var item = new DepartmentTreeItemViewModel(department);
                itemDict[department.Id] = item;
            }

            // Drugi przebieg - budowanie hierarchii
            foreach (var department in departments)
            {
                if (string.IsNullOrEmpty(department.ParentDepartmentId))
                {
                    // Root department
                    rootItems.Add(itemDict[department.Id]);
                }
                else if (itemDict.ContainsKey(department.ParentDepartmentId))
                {
                    // Child department
                    var parent = itemDict[department.ParentDepartmentId];
                    var child = new DepartmentTreeItemViewModel(department, parent);
                    parent.Children.Add(child);
                    
                    // Aktualizujemy referencję w słowniku
                    itemDict[department.Id] = child;
                }
            }

            // Sortowanie według SortOrder
            SortTree(rootItems);

            return rootItems;
        }

        /// <summary>
        /// Znajduje element w drzewie po ID
        /// </summary>
        public DepartmentTreeItemViewModel? FindItemById(IEnumerable<DepartmentTreeItemViewModel> items, string departmentId)
        {
            foreach (var item in items)
            {
                if (item.Id == departmentId)
                    return item;

                var found = FindItemById(item.Children, departmentId);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Sprawdza czy można przenieść dział (czy nie utworzy cyklu)
        /// </summary>
        public bool CanMoveDepartment(DepartmentTreeItemViewModel source, DepartmentTreeItemViewModel target)
        {
            if (source == null || target == null)
                return false;

            // Nie można przenieść działu do samego siebie
            if (source.Id == target.Id)
                return false;

            // Nie można przenieść działu do swojego potomka
            return !IsDescendant(source, target);
        }

        private bool IsDescendant(DepartmentTreeItemViewModel parent, DepartmentTreeItemViewModel potentialChild)
        {
            foreach (var child in parent.Children)
            {
                if (child.Id == potentialChild.Id)
                    return true;

                if (IsDescendant(child, potentialChild))
                    return true;
            }

            return false;
        }

        private void SortTree(List<DepartmentTreeItemViewModel> items)
        {
            var sorted = items.OrderBy(i => i.Department.SortOrder)
                             .ThenBy(i => i.Name)
                             .ToList();

            items.Clear();
            items.AddRange(sorted);

            foreach (var item in items)
            {
                if (item.Children.Count > 0)
                {
                    var childrenList = item.Children.ToList();
                    SortTree(childrenList);
                    item.Children.Clear();
                    foreach (var child in childrenList)
                    {
                        item.Children.Add(child);
                    }
                }
            }
        }
    }
} 