using QingtingRadio.DataModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace QingtingRadio
{
    public class SearchItemDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SearchRadioTemplate { get; set; }
        public DataTemplate SearchProgramTemplate { get; set; }
        public DataTemplate SearchAlbumTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is SearchItemRadio)
            {
                return SearchRadioTemplate;
            }
            else if (item is SearchItemProgram)
            {
                return SearchProgramTemplate;
            }
            else if(item is SearchItemAlbum)
            {
                return SearchAlbumTemplate;
            }

            return base.SelectTemplateCore(item);
        }
    }
}
