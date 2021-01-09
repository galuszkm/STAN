using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ComponentModel;

namespace STAN_PrePost
{
    public class TreeViewModel : INotifyPropertyChanged
    {
        TreeViewModel(string name, string arrayname)
        {
            Name = name;
            Children = new List<TreeViewModel>();
            ArrayName = arrayname;
            
        }

        #region Properties

        public string Name { get; private set; }
        public List<TreeViewModel> Children { get; private set; }
        public bool IsInitiallySelected { get; private set; }
        public string ArrayName { get; private set; }

        bool? _isChecked = false;
        TreeViewModel _parent;

        #region IsChecked

        public bool? IsChecked
        {
            get { return _isChecked; }
            set { SetIsChecked(value, true, true); }
        }

        void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked) return;

            _isChecked = value;

            if (updateChildren && _isChecked.HasValue) Children.ForEach(c => c.SetIsChecked(_isChecked, true, false));

            if (updateParent && _parent != null) _parent.VerifyCheckedState();

            NotifyPropertyChanged("IsChecked");
        }

        public void VerifyCheckedState()
        {
            bool? state = null;

            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }

            SetIsChecked(state, false, true);
        }

        #endregion

        #endregion

        void Initialize()
        {
            foreach (TreeViewModel child in Children)
            {
                child._parent = this;
                child.Initialize();
            }
        }

        public static List<TreeViewModel> SetTemplate()
        {
            List<TreeViewModel> Tree = new List<TreeViewModel>();

            TreeViewModel Displacement = new TreeViewModel("Displacement","");

            Displacement.Children.Add(new TreeViewModel("X", "Displacement X"));
            Displacement.Children.Add(new TreeViewModel("Y", "Displacement Y"));
            Displacement.Children.Add(new TreeViewModel("Z", "Displacement Z"));
            Displacement.Children.Add(new TreeViewModel("Total", "Total Displacement") { IsChecked = true });

            TreeViewModel Stress = new TreeViewModel("Stress", "");
            Stress.Children.Add(new TreeViewModel("XX", "Stress XX"));
            Stress.Children.Add(new TreeViewModel("YY", "Stress YY"));
            Stress.Children.Add(new TreeViewModel("ZX", "Stress ZX"));
            Stress.Children.Add(new TreeViewModel("XY", "Stress XY"));
            Stress.Children.Add(new TreeViewModel("YZ", "Stress YZ"));
            Stress.Children.Add(new TreeViewModel("XZ", "Stress XZ"));
            Stress.Children.Add(new TreeViewModel("P1", "Stress P1") { IsChecked = true });
            Stress.Children.Add(new TreeViewModel("P2", "Stress P2"));
            Stress.Children.Add(new TreeViewModel("P3", "Stress P3") { IsChecked = true });
            Stress.Children.Add(new TreeViewModel("von Mises", "von Mises Stress") { IsChecked = true, });

            TreeViewModel Strain = new TreeViewModel("Strain", "");
            Strain.Children.Add(new TreeViewModel("XX", "Strain XX"));
            Strain.Children.Add(new TreeViewModel("YY", "Strain YY"));
            Strain.Children.Add(new TreeViewModel("ZX", "Strain ZX"));
            Strain.Children.Add(new TreeViewModel("XY", "Strain XY"));
            Strain.Children.Add(new TreeViewModel("YZ", "Strain YZ"));
            Strain.Children.Add(new TreeViewModel("XZ", "Strain XZ"));
            Strain.Children.Add(new TreeViewModel("P1", "Strain P1"));
            Strain.Children.Add(new TreeViewModel("P2", "Strain P2"));
            Strain.Children.Add(new TreeViewModel("P3", "Strain P3"));
            Strain.Children.Add(new TreeViewModel("Effective", "Effective Strain") { IsChecked = true });

            Displacement.Initialize();
            Stress.Initialize();
            Strain.Initialize();

            Tree.Add(Displacement);
            Tree.Add(Stress);
            Tree.Add(Strain);


            return Tree;
        }

        public  List<string> GetSelectedItems()
        {
            List<string> selected = new List<string>();

            foreach(TreeViewModel item in Children)
            {
                if (item.IsChecked == true)
                {
                    selected.Add(item.ArrayName);
                }
            }
           
            return selected;
        }

        #region INotifyPropertyChanged Members

        void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}