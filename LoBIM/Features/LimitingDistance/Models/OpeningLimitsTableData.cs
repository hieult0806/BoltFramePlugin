using System.Collections.Generic;
using System.ComponentModel;

namespace LoBIM.Features.LimitingDistance.Models
{
    /// <summary>
    /// Container for both opening limits tables
    /// </summary>
    public class OpeningLimitsTableData
    {
        public List<TableDRow> TableD { get; set; } = new List<TableDRow>();
        public List<TableERow> TableE { get; set; } = new List<TableERow>();
    }

    /// <summary>
    /// Table 3.2.3.1.-D: Unprotected Opening Limits for Groups A, B, C, D and F, Division 3 Occupancies
    /// </summary>
    public class TableDRow : INotifyPropertyChanged
    {
        private string _maxArea;
        private string _d0;
        private string _d1_2;
        private string _d1_5;
        private string _d2_0;
        private string _d2_5;
        private string _d3;
        private string _d4;
        private string _d5;
        private string _d6;
        private string _d7;
        private string _d8;
        private string _d9;

        public string MaxArea
        {
            get => _maxArea;
            set { _maxArea = value; OnPropertyChanged(nameof(MaxArea)); }
        }

        public string D0
        {
            get => _d0;
            set { _d0 = value; OnPropertyChanged(nameof(D0)); }
        }

        public string D1_2
        {
            get => _d1_2;
            set { _d1_2 = value; OnPropertyChanged(nameof(D1_2)); }
        }

        public string D1_5
        {
            get => _d1_5;
            set { _d1_5 = value; OnPropertyChanged(nameof(D1_5)); }
        }

        public string D2_0
        {
            get => _d2_0;
            set { _d2_0 = value; OnPropertyChanged(nameof(D2_0)); }
        }

        public string D2_5
        {
            get => _d2_5;
            set { _d2_5 = value; OnPropertyChanged(nameof(D2_5)); }
        }

        public string D3
        {
            get => _d3;
            set { _d3 = value; OnPropertyChanged(nameof(D3)); }
        }

        public string D4
        {
            get => _d4;
            set { _d4 = value; OnPropertyChanged(nameof(D4)); }
        }

        public string D5
        {
            get => _d5;
            set { _d5 = value; OnPropertyChanged(nameof(D5)); }
        }

        public string D6
        {
            get => _d6;
            set { _d6 = value; OnPropertyChanged(nameof(D6)); }
        }

        public string D7
        {
            get => _d7;
            set { _d7 = value; OnPropertyChanged(nameof(D7)); }
        }

        public string D8
        {
            get => _d8;
            set { _d8 = value; OnPropertyChanged(nameof(D8)); }
        }

        public string D9
        {
            get => _d9;
            set { _d9 = value; OnPropertyChanged(nameof(D9)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Table 3.2.3.1.-E: Unprotected Opening Limits for Groups E and F, Division 1 and 2 Occupancies
    /// </summary>
    public class TableERow : INotifyPropertyChanged
    {
        private string _maxArea;
        private string _e0;
        private string _e1_2;
        private string _e1_5;
        private string _e2_0;
        private string _e2_5;
        private string _e3;
        private string _e4;
        private string _e5;
        private string _e6;
        private string _e7;
        private string _e8;
        private string _e9;
        private string _e10;
        private string _e11;
        private string _e12;
        private string _e13;
        private string _e14;
        private string _e15;

        public string MaxArea
        {
            get => _maxArea;
            set { _maxArea = value; OnPropertyChanged(nameof(MaxArea)); }
        }

        public string E0
        {
            get => _e0;
            set { _e0 = value; OnPropertyChanged(nameof(E0)); }
        }

        public string E1_2
        {
            get => _e1_2;
            set { _e1_2 = value; OnPropertyChanged(nameof(E1_2)); }
        }

        public string E1_5
        {
            get => _e1_5;
            set { _e1_5 = value; OnPropertyChanged(nameof(E1_5)); }
        }

        public string E2_0
        {
            get => _e2_0;
            set { _e2_0 = value; OnPropertyChanged(nameof(E2_0)); }
        }

        public string E2_5
        {
            get => _e2_5;
            set { _e2_5 = value; OnPropertyChanged(nameof(E2_5)); }
        }

        public string E3
        {
            get => _e3;
            set { _e3 = value; OnPropertyChanged(nameof(E3)); }
        }

        public string E4
        {
            get => _e4;
            set { _e4 = value; OnPropertyChanged(nameof(E4)); }
        }

        public string E5
        {
            get => _e5;
            set { _e5 = value; OnPropertyChanged(nameof(E5)); }
        }

        public string E6
        {
            get => _e6;
            set { _e6 = value; OnPropertyChanged(nameof(E6)); }
        }

        public string E7
        {
            get => _e7;
            set { _e7 = value; OnPropertyChanged(nameof(E7)); }
        }

        public string E8
        {
            get => _e8;
            set { _e8 = value; OnPropertyChanged(nameof(E8)); }
        }

        public string E9
        {
            get => _e9;
            set { _e9 = value; OnPropertyChanged(nameof(E9)); }
        }

        public string E10
        {
            get => _e10;
            set { _e10 = value; OnPropertyChanged(nameof(E10)); }
        }

        public string E11
        {
            get => _e11;
            set { _e11 = value; OnPropertyChanged(nameof(E11)); }
        }

        public string E12
        {
            get => _e12;
            set { _e12 = value; OnPropertyChanged(nameof(E12)); }
        }

        public string E13
        {
            get => _e13;
            set { _e13 = value; OnPropertyChanged(nameof(E13)); }
        }

        public string E14
        {
            get => _e14;
            set { _e14 = value; OnPropertyChanged(nameof(E14)); }
        }

        public string E15
        {
            get => _e15;
            set { _e15 = value; OnPropertyChanged(nameof(E15)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
