using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;

namespace Peekerino.Shell
{
    public class ExplorerSelectionProvider
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        public bool TryGetSelectedExplorerItemPath(out string? selectedPath)
        {
            selectedPath = null;

            IntPtr explorerHandle = GetForegroundWindow();
            if (explorerHandle == IntPtr.Zero)
            {
                return false;
            }

            var className = new StringBuilder(256);
            if (GetClassName(explorerHandle, className, className.Capacity) == 0)
            {
                return false;
            }

            string windowClass = className.ToString();
            if (!windowClass.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) &&
                !windowClass.Equals("ExplorerWClass", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
            {
                return false;
            }

            object? shell = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                if (shell == null)
                {
                    return false;
                }

                dynamic shellDynamic = shell;
                object? windowsCom = null;
                IEnumerable? windowsEnumerable = null;

                try
                {
                    windowsCom = shellDynamic.Windows();
                    windowsEnumerable = windowsCom as IEnumerable;
                    if (windowsEnumerable == null)
                    {
                        return false;
                    }

                    foreach (object windowObj in windowsEnumerable)
                    {
                        dynamic window = windowObj;
                        object? documentObj = null;
                        object? selectedItemsObj = null;

                        try
                        {
                            long hwndValue = Convert.ToInt64(window.HWND);
                            if (hwndValue == 0 || new IntPtr(hwndValue) != explorerHandle)
                            {
                                continue;
                            }

                            documentObj = window.Document;
                            if (documentObj == null)
                            {
                                return false;
                            }

                            dynamic document = documentObj;
                            selectedItemsObj = document.SelectedItems();
                            if (selectedItemsObj == null)
                            {
                                return false;
                            }

                            dynamic selectedItems = selectedItemsObj;
                            int count = Convert.ToInt32(selectedItems.Count);
                            if (count == 0)
                            {
                                return false;
                            }

                            dynamic firstItem = selectedItems.Item(0);
                            selectedPath = firstItem?.Path as string;
                            return !string.IsNullOrEmpty(selectedPath);
                        }
                        finally
                        {
                            ReleaseComObject(selectedItemsObj);
                            ReleaseComObject(documentObj);
                            ReleaseComObject(windowObj);
                        }
                    }
                }
                finally
                {
                    ReleaseComObject(windowsCom);
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                ReleaseComObject(shell);
            }

            return false;
        }

        private static void ReleaseComObject(object? comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
    }
}

