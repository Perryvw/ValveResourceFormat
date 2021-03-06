﻿using GUI.Forms;
using System;
using System.Windows.Forms;
using ValveResourceFormat;
using GUI.Utils;
using System.Drawing;

namespace GUI.Controls
{
    /// <summary>
    /// Represents a user control in which a TreeView and ListView are used to view a directory/file listing. In addition to a normal TreeView,
    /// this control allows for searching to occur within the TreeView and have the results displayed in a ListView with details about the resulting
    /// items.
    /// </summary>
    public partial class TreeViewWithSearchResults : UserControl
    {
        private ImageList imageList;

        public TreeNodeMouseClickEventHandler TreeNodeMouseDoubleClick;             // when a TreeNode is double clicked
        public TreeNodeMouseClickEventHandler TreeNodeMouseClick;                   // when a TreeNode is single clicked
        public EventHandler<ListViewItemClickEventArgs> ListViewItemDoubleClick;    // when a ListViewItem is double clicked
        public EventHandler<ListViewItemClickEventArgs> ListViewItemRightClick;     // when a ListViewItem is single clicked

        /// <summary>
        /// Constructor to require an image list for display on listed TreeView nodes and ListView items.
        /// </summary>
        /// <param name="imageList"></param>
        public TreeViewWithSearchResults(ImageList imageList) : this()
        {
            this.imageList = imageList;
        }

        /// <summary>
        /// Require a default constructor for the designer.
        /// </summary>
        public TreeViewWithSearchResults()
        {
            InitializeComponent();

            mainListView.MouseDoubleClick += MainListView_MouseDoubleClick;
            mainListView.MouseDown += MainListView_MouseDown;
            mainListView.Resize += MainListView_Resize;
            mainListView.FullRowSelect = true;

            mainTreeView.HideSelection = false;

            mainTreeView.NodeMouseDoubleClick += (o, e) =>
            {
                if (TreeNodeMouseDoubleClick != null) { TreeNodeMouseDoubleClick(o, e); }
            };

            mainTreeView.NodeMouseClick += (o, e) =>
            {
                if (TreeNodeMouseClick != null)
                {
                    // if user left clicked a folder, show the contents of that folder in the list view
                    if (e.Button == MouseButtons.Left && e.Node.Tag is TreeViewFolder)
                    {
                        mainListView.Items.Clear();
                        foreach (TreeNode node in e.Node.Nodes)
                        {
                            AddNodeToListView(node);
                        }
                    }

                    TreeNodeMouseClick(o, e);
                }
            };
        }

        private void MainListView_Resize(object sender, EventArgs e)
        {
            ResizeListViewColumns();
        }

        private void ResizeListViewColumns()
        {
            foreach (ColumnHeader col in mainListView.Columns)
            {
                if (col.Text == "Name")
                {
                    col.Width = mainListView.ClientSize.Width / 2;
                }
                else
                {
                    col.Width = mainListView.ClientSize.Width / 4;
                }
            }
        }

        /// <summary>
        /// Initializes the TreeView in the control with the contents of the passed Package. Contents are sorted and expanded by default.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="package"></param>
        internal void InitializeTreeViewFromPackage(string name, Package package)
        {
            mainListView.Tag = package;

            var control = mainTreeView;
            control.Name = name;
            control.Tag = package; //so we can access it later
            control.Dock = DockStyle.Fill;
            control.ImageList = imageList;

            //http://stackoverflow.com/a/24591871

            foreach (var fileType in package.Entries)
            {
                foreach (var file in fileType.Value)
                {
                    control.AddFileNode(file, fileType);
                }
            }

            control.Sort();
            control.ExpandAll();
        }

        /// <summary>
        /// Performs a search for the entered text and search types. Before a search is performed, the contents of the ListView (previous search results) are cleared.
        /// Results of whatever search function is used are displayed in the ListView with name, file size, and file type.
        /// </summary>
        /// <param name="searchText"></param>
        /// <param name="selectedSearchType"></param>
        internal void SearchAndFillResults(string searchText, bool isCaseSensitive, SearchType selectedSearchType)
        {
            mainListView.Items.Clear();

            var results = mainTreeView.Search(searchText, isCaseSensitive, selectedSearchType);

            foreach (TreeNode node in results)
            {
                AddNodeToListView(node);
            }

            ResizeListViewColumns();
        }

        /// <summary>
        /// When the user clicks in the ListView, check if the user clicks outside of a ListViewItem. If so, de-select any previously selected ListViewItems. In addition,
        /// if the user right clicked an item in the ListView, let our subscribers know what was clicked and where in case a context menu is needed to be shown.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainListView_MouseDown(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo info = mainListView.HitTest(e.X, e.Y);

            // if an item was clicked in the list view
            if (info.Item != null)
            {
                // right click should just notify our subscribers
                if (e.Button == MouseButtons.Right)
                {
                    if (ListViewItemRightClick != null)
                    {
                        ListViewItemRightClick(sender, new ListViewItemClickEventArgs(info.Item, e.Location));
                    }
                }
                // left click should focus the node in its tree view
                else if (e.Button == MouseButtons.Left)
                {
                    var node = info.Item.Tag as TreeNode;
                    if (node.Tag is TreeViewFolder)
                    {
                        node.EnsureVisible();
                        node.TreeView.SelectedNode = node;
                    }
                }
            }
            else
            {
                mainListView.SelectedItems.Clear();
            }
        }

        /// <summary>
        /// If the user double clicks (with left mouse button) on a ListViewItem, send up an event to subscribers that such an action has occurred. Also send up
        /// whatever object is represented by the ListViewItem.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ListViewHitTestInfo info = mainListView.HitTest(e.X, e.Y);

                if (info.Item != null)
                {
                    // if user left double clicks a folder, open its contents and display in list view
                    var node = info.Item.Tag as TreeNode;
                    if (node.Tag is TreeViewFolder)
                    {
                        mainListView.Items.Clear();
                        foreach(TreeNode childNode in node.Nodes)
                        {
                            AddNodeToListView(childNode);
                        }
                    }

                    if (ListViewItemDoubleClick != null)
                    {
                        ListViewItemDoubleClick(sender, new ListViewItemClickEventArgs(info.Item.Tag));
                    }
                }
                else
                {
                    mainListView.SelectedItems.Clear();
                }
            }
        }

        /// <summary>
        /// When the form loads, create the columns that we want to see such as name, file size, and file type.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewWithSearchResults_Load(object sender, EventArgs e)
        {
            mainListView.Columns.Add("Name");
            mainListView.Columns.Add("Size");
            mainListView.Columns.Add("Type");
            mainListView.SmallImageList = imageList;
        }

        private void AddNodeToListView(TreeNode node)
        {
            ListViewItem item = new ListViewItem(node.Text);
            item.ImageKey = node.ImageKey;
            item.Tag = node;

            if (node.Tag.GetType() == typeof(PackageEntry))
            {
                var file = node.Tag as PackageEntry;
                item.SubItems.Add(file.TotalLength.ToFileSizeString());
                item.SubItems.Add(file.TypeName);
            }
            else if (node.Tag.GetType() == typeof(Controls.TreeViewFolder))
            {
                var folder = node.Tag as TreeViewFolder;
                item.SubItems.Add(String.Format("{0} items", folder.ItemCount));
                item.SubItems.Add("folder");
            }

            mainListView.Items.Add(item);
        }
    }
}