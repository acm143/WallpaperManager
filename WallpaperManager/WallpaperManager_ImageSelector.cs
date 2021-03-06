﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LanceTools.FormUtil;
using WallpaperManager.ApplicationData;
using WallpaperManager.ImageSelector;
using WallpaperManager.Options;
using LanceTools.FormUtil;

namespace WallpaperManager
{
    public partial class WallpaperManager : Form
    {
        private string[] selectedImages;
        private int imagesPerPage;

        private float initialLabelSelectedImageFontSize;

        private Queue<FlowLayoutPanel> loadedTabs = new Queue<FlowLayoutPanel>();
        private Queue<Image> loadedImages = new Queue<Image>();
        private int maxLoadedTabs;

        private FlowLayoutPanel activeTabLayoutPanel;

        private void tabLayoutPanel_MouseClick(object sender, EventArgs e)
        {
            (sender as ScrollLockedFlowLayoutPanel).Focus();
        }

        private void buttonClearSelection_Click(object sender, EventArgs e)
        {
            ClearImageSelector();
        }

        private void ClearImageSelector()
        {
            panelImageSelector.Visible = false;

            inspectedImage = "";
            selectedImages = null;
            DeactivateImageInspector();

            labelSelectedImage.Text = "Select an image for more info";
            labelImageSize.Text = "";

            tabControlImagePages.SuspendLayout();
            tabControlImagePages.Flush();
            tabControlImagePages.Refresh();
            tabControlImagePages.ResumeLayout();
        }

        private void InitializeImageSelector()
        {
            tabControlImagePages.SelectedIndexChanged += LoadTabEvent;
            labelSelectedImage.TextChanged += labelSelectedImage_TextChanged;
            initialLabelSelectedImageFontSize = labelSelectedImage.Font.Size;
            labelImageSize.Text = "";
        }

        public void RebuildImageSelector(string[] selectedImages, int imagesPerPage = 30, int maxLoadedTabs = 25)
        {
            if (selectedImages == null)
            {
                MessageBox.Show("No images were selected");
                return;
            }

            int invalidCounter = 0;
            for (int i = 0; i < selectedImages.Length; i++)
            {
                if (selectedImages[i] == null)
                {
                    invalidCounter++;
                }
            }

            if (invalidCounter == selectedImages.Length)
            {
                MessageBox.Show("No images were selected");
                return;
            }

            if (invalidCounter > 1)
            {
                MessageBox.Show("Error, some of the images selected were null");
                return;
            }

            inspectedImage = "";
            DeactivateImageInspector();
            labelSelectedImage.Text = "Select an image for more info";
            tabControlImagePages.SuspendLayout();
            tabControlImagePages.Flush();

            // Ensure that only enabled images are selected
            if (!OptionsData.EnableDetectionOfInactiveImages)
            {
                HashSet<string> activeSelectedImages = new HashSet<string>();
                foreach (string image in selectedImages)
                {
                    if (WallpaperData.ContainsImage(image))
                    {
                        if (WallpaperData.GetImageData(image).Active)
                        {
                            activeSelectedImages.Add(image);
                        }
                    }
                }

                selectedImages = activeSelectedImages.ToArray();
            }

            if (selectedImages.Length == 0)
            {
                MessageBox.Show("No images were selected");
                return;
            }

            int pageCount = (selectedImages.Length / imagesPerPage) + 1;

            for (int i = 0; i < pageCount; i++)
            {
                tabControlImagePages.TabPages.Add((i + 1).ToString() + " Key", (i + 1).ToString());

                TabPage curTab = tabControlImagePages.TabPages[i];
                curTab.BackColor = Color.Black;
            }

            // Finalization
            this.selectedImages = selectedImages;
            this.imagesPerPage = imagesPerPage;
            this.maxLoadedTabs = maxLoadedTabs;

            LoadTab(0);
            //tabControlImagePages.Refresh();
            tabControlImagePages.ResumeLayout();

            panelImageSelector.Visible = true;
        }

        private void LoadTabEvent(object sender, EventArgs e)
        {
            LoadTab(tabControlImagePages.SelectedIndex);
        }

        private void LoadTab(int tabIndex)
        {
            if (tabIndex != -1 && selectedImages != null) // tabIndex will equal -1 when resetting the tabControl | selectedImages may equal null when clearing
            {
                tabControlImagePages.SuspendLayout();
                while (loadedImages.Count > 0)
                {
                    loadedImages.Dequeue()?.Dispose();
                }

                // Add controls to their panels
                activeTabLayoutPanel?.Dispose();

                ScrollLockedFlowLayoutPanel tabLayoutPanel = new ScrollLockedFlowLayoutPanel();
                tabLayoutPanel.Size = new Size(tabControlImagePages.Size.Width - 5, tabControlImagePages.Size.Height - tabControlImagePages.ItemSize.Height - 5);
                tabLayoutPanel.AutoScroll = true;
                tabLayoutPanel.HorizontalScroll.Visible = false;
                tabLayoutPanel.MouseClick += tabLayoutPanel_MouseClick;

                if (tabControlImagePages.TabPages[tabIndex].Controls.Count > 0)
                {
                    tabControlImagePages.TabPages[tabIndex].Controls.RemoveAt(0);
                }

                tabControlImagePages.TabPages[tabIndex].Controls.Add(tabLayoutPanel); // panel added here

                // Load in image editors
                int indexOffset = tabIndex * imagesPerPage;
                int maxIndex = imagesPerPage + indexOffset - 1;
                maxIndex = maxIndex < selectedImages.Length ? maxIndex : selectedImages.Length;

                string invalidImageSelection = "The following images are not included in your theme: ";
                string invalidImageSelectionDefault = invalidImageSelection;

                tabLayoutPanel.SuspendLayout();
                for (int i = indexOffset; i <= maxIndex; i++)
                {
                    if (i != selectedImages.Length)
                    {
                        if (selectedImages[i] != null && File.Exists(selectedImages[i]) && WallpaperData.ContainsImage(selectedImages[i]))
                        {
                            tabLayoutPanel.Controls.Add(new ImageEditorControl(WallpaperData.GetImageData(selectedImages[i])));
                        }
                        else
                        {
                            invalidImageSelection += "\n" + selectedImages[i];
                        }
                    }
                }
                tabLayoutPanel.ResumeLayout();

                tabLayoutPanel.Focus(); // clicking on the Tab Control to change pages loses the focus of the panel
                activeTabLayoutPanel = tabLayoutPanel;
                //tabControlImagePages.Refresh();
                tabControlImagePages.ResumeLayout();

                if (invalidImageSelectionDefault != invalidImageSelection)
                {
                    MessageBox.Show(invalidImageSelection);
                }
            }
        }

        public void UpdateSelectedImage(WallpaperData.ImageData imageData)
        {
            labelSelectedImage.Text = inspectedImage = imageData.Path;
        }

        private void labelSelectedImage_TextChanged(object sender, EventArgs e) // resize if bounds extend too far to the right
        {
            //TODO Figure out how to do this properly
            /*
            labelSelectedImage.Refresh();

            if (labelSelectedImage.Bounds.Right > panelImageSelector.Width)
            {
                int actualBoundsRight = labelSelectedImage.Bounds.Right + panelImageSelector.Width;
                float newSize = (float)Math.Floor(((initialLabelSelectedImageFontSize * (float) this.Width / actualBoundsRight) - 0.25f));
                Debug.WriteLine(newSize);
                Font resizedFont = new Font
                (
                    labelSelectedImage.Font.FontFamily,
                    newSize,//initialLabelSelectedImageFontSize * (float)this.Bounds.Right / actualBoundsRight,
                    labelSelectedImage.Font.Style,
                    labelSelectedImage.Font.Unit,
                    labelSelectedImage.Font.GdiCharSet
                );

                labelSelectedImage.Font = resizedFont;
            }
            else if (labelSelectedImage.Font.Size != initialLabelSelectedImageFontSize)
            {
                Font resizedFont = new Font
                (
                    labelSelectedImage.Font.FontFamily,
                    initialLabelSelectedImageFontSize,
                    labelSelectedImage.Font.Style,
                    labelSelectedImage.Font.Unit,
                    labelSelectedImage.Font.GdiCharSet
                );

                labelSelectedImage.Font = resizedFont;
            }
            */
        }

        public void LoadImage(ImageEditorControl parentEditorControl, string imagePath)
        {
            Thread thread = new Thread(() =>
            {
                Image image = Image.FromFile(imagePath);
                loadedImages.Enqueue(image);
                parentEditorControl.SetBackgroundImage(image);
            });
            thread.Start();
        }

        public string GetActiveImage()
        {
            return inspectedImage ?? "";
        }

        public string[] GetSelectedImages()
        {
            return selectedImages ?? new string[0];
        }

        public void UpdateImageRanks()
        {
            if (activeTabLayoutPanel != null)
            {
                foreach (Control control in activeTabLayoutPanel.Controls)
                {
                    (control as ImageEditorControl)?.UpdateRank();
                }
            }
        }
    }
}
