using System.Collections.Generic;
using System.ComponentModel;
using Windows.ApplicationModel.Background;
using Windows.Security.Cryptography.Core;
using Windows.UI.Xaml;

namespace QingtingRadio.ViewModels
{
    /// <summary>
    /// 页面 或 控件 的单一属性的绑定，非特定于某一个页面/控件
    /// </summary>
    public class RadioSingleProperty : INotifyPropertyChanged
    {
        private bool _hasCustomRadio;
        private Visibility _panelVisibility;


        public Visibility PanelVisibility
        {
            get { return _panelVisibility; }
            set
            {
                if (_panelVisibility != value)
                {
                    _panelVisibility = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelVisibility)));
                }
            }
        }       

        public bool HasCustomRadio
        {
            get
            {
                return _hasCustomRadio;
            }
            set
            {
                _hasCustomRadio = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasCustomRadio)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private RadioSingleProperty() { }
        public static readonly RadioSingleProperty Current = new RadioSingleProperty();
    }
}
