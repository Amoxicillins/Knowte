﻿using Digimezzo.Utilities.Utils;
using Digimezzo.WPFControls;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Knowte.Services.Dialog
{
    public partial class CustomDialog : BorderlessWindows10Window
    {
        private Func<Task<bool>> callback;

        public CustomDialog(string title, UserControl content, int width, int height, bool canResize, bool autoSize, bool showTitle, bool showCancelButton, string okText, string cancelText, Func<Task<bool>> callback) : base()
        {
            InitializeComponent();

            this.Title = title;
            this.TextBlockTitle.Text = title;
            this.Width = width;
            this.MinWidth = width;
            this.CustomContent.Content = content;

            if (canResize)
            {
                this.ResizeMode = ResizeMode.CanResize;
                this.Height = height;
                this.MinHeight = height;
                this.SizeToContent = SizeToContent.Manual;
            }
            else
            {
                this.ResizeMode = ResizeMode.NoResize;

                if (autoSize)
                {
                    if (width.Equals(0))
                    {
                        this.SizeToContent = SizeToContent.WidthAndHeight;
                    }
                    else
                    {
                        this.SizeToContent = SizeToContent.Height;
                    }
                }
                else
                {
                    this.Height = height;
                    this.MinHeight = height;
                    this.SizeToContent = SizeToContent.Manual;
                }
            }

            if (showCancelButton)
            {
                this.ButtonCancel.Visibility = Visibility.Visible;
            }
            else
            {
                this.ButtonCancel.Visibility = Visibility.Collapsed;
            }

            if (showTitle)
            {
                this.TitlePane.Visibility = Visibility.Visible;
            }
            else
            {
                this.TitlePane.Visibility = Visibility.Collapsed;
            }

            this.ButtonOK.Content = okText;
            this.ButtonCancel.Content = cancelText;

            this.callback = callback;

            WindowUtils.CenterWindow(this);
        }

        private async void ButtonOK_Click(Object sender, RoutedEventArgs e)
        {
            if (this.callback != null)
            {
                // Prevents clicking the buttons when the callback is already executing, and this prevents this Exception:
                // System.InvalidOperationException: DialogResult can be set only after Window is created and shown as dialog.
                this.ButtonOK.IsEnabled = false;
                this.ButtonOK.IsDefault = false;
                this.ButtonCancel.IsEnabled = false;
                this.ButtonCancel.IsCancel = false;

                // Execute some function in the caller of this dialog.
                // If the result is False, DialogResult is not set.
                // That keeps the dialog open.
                if (await this.callback.Invoke())
                {
                    DialogResult = true;
                }
                else
                {
                    this.ButtonOK.IsEnabled = true;
                    this.ButtonOK.IsDefault = true;
                    this.ButtonCancel.IsEnabled = true;
                    this.ButtonCancel.IsCancel = true;
                }
            }
            else
            {
                DialogResult = true;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
