using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Items;
public class TabItem( string title, IFileEntryViewModel root ) {
    public string Title { get; } = title;

    public IFileEntryViewModel Root { get; } = root;
}