using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace LoBIM.ViewModels.UserControls
{
    public class TreeViewUcVM : INotifyPropertyChanged
    {
        public ICommand ItemSelectedCommand { get; set; }
        private object _selectedTreeItem;
        public object SelectedTreeItem
        {
            get => _selectedTreeItem;
            set
            {
                _selectedTreeItem = value;
                OnPropertyChanged(nameof(SelectedTreeItem));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<CategoryModel> Categories { get; set; }
        private UIDocument _uidoc;
        public TreeViewUcVM(UIDocument uiDocument)
        {
            _uidoc = uiDocument;
            var service = new RevitSelectionService(uiDocument);
            var selectedElements = service.GetSelectedElements();
            Categories = CreateTreeFromSelectedElements(selectedElements);

            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            SetDefaultSelectedItem();
        }

        // Command handler when an item is selected
        private void OnItemSelected(object selectedItem)
        {
            // Handle the selected item logic here
            SelectedTreeItem = selectedItem;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Create a tree structure from the selected elements
        private ObservableCollection<CategoryModel> CreateTreeFromSelectedElements(List<Element> selectedElements)
        {
            var categories = new ObservableCollection<CategoryModel>();

            // Group elements by category (Wall, Floor, Roof)
            var wallCategory = new CategoryModel("Walls");
            var floorCategory = new CategoryModel("Floors");
            var roofCategory = new CategoryModel("Roofs");

            foreach (var element in selectedElements)
            {
                if (element is Wall)
                {
                    AddElementToCategory(wallCategory, element);
                }
                else if (element is Floor)
                {
                    AddElementToCategory(floorCategory, element);
                }
                else if (element is RoofBase)
                {
                    AddElementToCategory(roofCategory, element);
                }
            }

            if (wallCategory.Families.Count > 0) categories.Add(wallCategory);
            if (floorCategory.Families.Count > 0) categories.Add(floorCategory);
            if (roofCategory.Families.Count > 0) categories.Add(roofCategory);
            return categories;
        }

        // Method to set the first item as selected
        private void SetDefaultSelectedItem()
        {
            // Check if there are any categories, and select the first element as default
            if (Categories.Count > 0 && Categories[0].Families.Count > 0 && Categories[0].Families[0].Types.Count > 0)
            {
                // Select the first type of the first family in the first category
                SelectedTreeItem = Categories[0].Families[0].Types[0];
            }
        }

        // Add an element to a category based on its family and type
        private void AddElementToCategory(CategoryModel category, Element element)
        {
            // Get the ElementType (which contains family and type information)
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;

            if (elementType != null)
            {
                // Use the FamilyName if available, otherwise use the ElementType name as fallback
                var familyName = elementType.FamilyName ?? elementType.Name;
                var typeName = elementType.Name;

                // Find or create the family model
                var family = category.Families.FirstOrDefault(f => f.Name == familyName);
                if (family == null)
                {
                    family = new FamilyModel(familyName);
                    category.Families.Add(family);
                }

                // Add the type to the family
                family.Types.Add(new TypeModel($"{typeName}"));
            }
        }
    }

    public class CategoryModel
    {
        public string Name { get; set; }
        public ObservableCollection<FamilyModel> Families { get; set; }

        public CategoryModel(string name)
        {
            Name = name;
            Families = new ObservableCollection<FamilyModel>();
        }
    }

    public class FamilyModel
    {
        public string Name { get; set; }
        public ObservableCollection<TypeModel> Types { get; set; }

        public FamilyModel(string name)
        {
            Name = name;
            Types = new ObservableCollection<TypeModel>();
        }
    }

    public class TypeModel
    {
        public string Name { get; set; }

        public TypeModel(string name)
        {
            Name = name;
        }
    }
}
