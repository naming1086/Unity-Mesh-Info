using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Rendering;
using UObject = UnityEngine.Object;
using OfficeOpenXml;
using System.IO;
using System.Collections.Generic;

namespace MeshExtensions.Editor
{
    public class MeshInfoWindow : EditorWindow
    {
        [MenuItem("Tools/Mesh Info", false, 0)]
        public static void ShowWindow()
        {
            MeshInfoWindow window = GetWindow<MeshInfoWindow>();
            window.Show();
        }

        public Material _material;

        protected Mesh _mesh;
        protected TableView _tableView;
        protected MeshView _meshView;
        
        protected float _splitHeight = 0.0f;
        protected float _splitOffset = 0.0f;
        protected bool _resize = false;

        public static IList<int> Selected = default;

        private void OnEnable()
        {
            titleContent = new GUIContent("Mesh Info", EditorGUIUtility.IconContent("MainStageView").image);
            EditorApplication.searchChanged += Changed; //查找的时候的change
            
            _mesh = null;
            _tableView = null;
            _meshView = null;
            _splitHeight = EditorPrefs.GetFloat("mesh-info-split-height", 350f);

            Changed();
            Repaint();
        }
        
        private void OnDisable()
        {
            EditorApplication.searchChanged -= Changed;
            _meshView.Dispose();
            EditorPrefs.SetFloat("mesh-info-split-height", _splitHeight);
        }
        
        private void OnSelectionChange()
        {
            Changed();
        }

        private void OnGUI()
        {
            GUI.enabled = true;

            if (!_mesh)
            {
                EditorGUILayout.LabelField($"Select mesh to display information.");
                return;
            }
            exportExcel();
            ShowSplit();
            ShowTableView();
            ShowMaterialInput();
            ShowMeshInput();
            ShowMeshView();




            GUI.enabled = false;

            CheckSelection();
        }

        private void CheckSelection()
        {
            Selected = _tableView.HasSelection() ? _tableView.GetSelection() : default;
        }

        private void Changed()
        {
            if (!(Selection.activeObject is Mesh mesh))
            {
            }
            else if (mesh != _mesh)
            {
                _mesh = mesh;
                _meshView?.Dispose();
                CreateMeshView();
                CreateTable();
            }
            Repaint();
        }

        private void updateMeshView(){
            _meshView.updateShadeMaterial(_material);
        }

        private void ShowMeshView()
        {
            if (_meshView != null)
            {
                Rect r = new Rect(0, 40, position.width, position.height - (position.height - _splitHeight + 60.0f));
                _meshView.OnPreviewGUI(r, GUIStyle.none);

                r = new Rect(0, _splitHeight - 20.0f, position.width, 20.0f);
                _meshView.OnPreviewSettings(r);
            }
        }

        private void ShowTableView()
        {
            if (_tableView != null)
            {
                Rect r = new Rect(0, _splitHeight, position.width, position.height - _splitHeight);
                _tableView.OnGUI(r);
            }
        }
        
        private void ShowMaterialInput()
        {
            Rect r = new Rect(0, 0, position.width, 20);
            _material = (Material)EditorGUILayout.ObjectField("Material", _material, typeof(Material), false);  
        }

        private void ShowMeshInput()
        {
            Rect r = new Rect(0, 0, position.width, 20);
            _mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", _mesh, typeof(Mesh), false);    
        }



        private void ShowSplit()
        {
            Rect rect = new Rect(0.0f, _splitHeight, position.width, 4.0f);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.SplitResizeUpDown);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _splitOffset = Event.current.mousePosition.y - _splitHeight;
                _resize = true;
            }

            if (_resize)
            {
                _splitHeight = Mathf.Clamp(Event.current.mousePosition.y - _splitOffset, 100.0f, position.height - 100.0f);
                Repaint();
            }
            
            if (Event.current.type == EventType.MouseUp)
            {
                _resize = false;
            }
        }
        
        void exportExcel()
        {
            // int buttonHeight = 29;
            if (GUILayout.Button("Export Excel Data", GUILayout.Height(20)))
            {
                string exportPath = EditorUtility.SaveFilePanel("Save SkuInfo File", "", "MeshData-"+".xlsx", "xlsx");
                if (string.IsNullOrEmpty(exportPath))
                {
                    return;
                }
                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                using (ExcelPackage package = new ExcelPackage(new FileInfo(exportPath)))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Sheet1");
                    worksheet.Column(1).Width = 30;
                    for (int i = 2; i <= 13; i++)
                    {
                        worksheet.Column(i).Width = 13;
                        worksheet.Column(i).Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    }

                    VertexAttributeDescriptor[] attributes = _mesh.GetVertexAttributes();
                    int vertexCount = _mesh.vertexCount;

                    MultiColumnHeaderState.Column[] columns = CreateColumns(attributes);
                    MultiColumnHeaderState headerstate = new MultiColumnHeaderState(columns);
                    MultiColumnHeader header = new MultiColumnHeader(headerstate);
                    header.canSort = false;

                    int intA = 1;
                    worksheet.Cells[1, intA].Value = "INX";
                    intA += 1;
                    foreach (VertexAttributeDescriptor a in attributes){
                        for (int i = 0; i < a.dimension; i++)
                        {
                            worksheet.Cells[1, intA].Value = a.attribute.ToString();
                            worksheet.Cells[1, intA].Style.Font.Bold = true;
                            worksheet.Cells[1, intA].Style.Font.Size = 12;
                            intA += 1;
                        }
                    }


                    for (int i = 0; i < vertexCount; i++)
                    {
                        object[] properties = CreateRow(i, attributes);
                        for (int ii = 0; ii < properties.Length; ii++)
                        {
                            worksheet.Cells[i + 2, ii + 1].Value = properties[ii];
                            worksheet.Cells[i + 2, ii + 1].Style.Font.Bold = true;
                            worksheet.Cells[i + 2, ii + 1].Style.Font.Size = 12;
                        }
                    }
                    
                    package.Save();
                    exportPath = exportPath.Replace('/', '\\');
#if UNITY_ED_WIN
                    System.Diagnostics.Process p = new System.Diagnostics.Process();
                    p.StartInfo.FileName = "explorer.exe";
                    p.StartInfo.Arguments = (@" /select," + exportPath);
                    p.Start();
#endif
                }
            }
        }



        private void CreateTable()
        {
            TreeViewState state = new TreeViewState();
            VertexAttributeDescriptor[] attributes = _mesh.GetVertexAttributes();
            int vertexCount = _mesh.vertexCount;

            MultiColumnHeaderState.Column[] columns = CreateColumns(attributes);
            MultiColumnHeaderState headerstate = new MultiColumnHeaderState(columns);
            MultiColumnHeader header = new MultiColumnHeader(headerstate);
            header.canSort = false;

            _tableView = new TableView(state, header);
            _tableView.Reload();

            bool ifOver = vertexCount > 2001;
            int maxVertexCount = ifOver ? 2001 : vertexCount;
            for (int i = 0; i < maxVertexCount; i++) //vertexCount 
            {
                object[] properties = CreateRow(i, attributes);
                _tableView.AddElement(properties);
            }



            _tableView.Repaint();
        }

        private void CreateMeshView()
        {
            _meshView = new MeshView(_mesh, _material);
        }

        private object[] CreateRow(int idx, VertexAttributeDescriptor[] attributes)
        {
            List<object> rows = new List<object>();
            rows.Add(idx);
            
            List<Vector2> vector2s = new List<Vector2>();
            List<Vector3> vector3s = new List<Vector3>();
            List<Vector4> vector4s = new List<Vector4>();

            foreach (VertexAttributeDescriptor a in attributes)
            {
                if (a.attribute == VertexAttribute.BlendIndices || a.attribute == VertexAttribute.BlendWeight)
                    continue;

                int uVId = -1;
                
                switch (a.attribute)
                {
                    case VertexAttribute.Position:
                        rows.Add(_mesh.vertices[idx].x);
                        rows.Add(_mesh.vertices[idx].y);
                        rows.Add(_mesh.vertices[idx].z);
                        break;
                    case VertexAttribute.Normal:
                        rows.Add(_mesh.normals[idx].x);
                        rows.Add(_mesh.normals[idx].y);
                        rows.Add(_mesh.normals[idx].z);
                        break;
                    case VertexAttribute.Tangent:
                        rows.Add(_mesh.tangents[idx].x);
                        rows.Add(_mesh.tangents[idx].y);
                        rows.Add(_mesh.tangents[idx].z);
                        rows.Add(_mesh.tangents[idx].w);
                        break;
                    case VertexAttribute.Color:
                        if (_mesh.colors != null && _mesh.colors.Length > 0)
                        {
                            rows.Add(_mesh.colors[idx].r);
                            rows.Add(_mesh.colors[idx].g);
                            rows.Add(_mesh.colors[idx].b);
                            rows.Add(_mesh.colors[idx].a);
                        }
                        else if (_mesh.colors32 != null && _mesh.colors32.Length > 0)
                        {
                            rows.Add(_mesh.colors32[idx].r);
                            rows.Add(_mesh.colors32[idx].g);
                            rows.Add(_mesh.colors32[idx].b);
                            rows.Add(_mesh.colors32[idx].a);
                        }
                        break;
                    case VertexAttribute.TexCoord0:
                        uVId = 0;
                        break;
                    case VertexAttribute.TexCoord1:
                        uVId = 1;
                        break;
                    case VertexAttribute.TexCoord2:
                        uVId = 2;
                        break;
                    case VertexAttribute.TexCoord3:
                        uVId = 3;
                        break;
                    case VertexAttribute.TexCoord4:
                        uVId = 4;
                        break;
                    case VertexAttribute.TexCoord5:
                        uVId = 5;
                        break;
                    case VertexAttribute.TexCoord6:
                        uVId = 6;
                        break;
                    case VertexAttribute.TexCoord7:
                        uVId = 7;
                        break;
                }

                if (uVId == -1)
                    continue;
                
                switch (a.dimension)
                {
                    case 2:
                        _mesh.GetUVs(uVId, vector2s);
                        rows.Add(vector2s[idx].x);
                        rows.Add(vector2s[idx].y);
                        break;
                    case 3:
                        _mesh.GetUVs(uVId, vector3s);
                        rows.Add(vector3s[idx].x);
                        rows.Add(vector3s[idx].y);
                        rows.Add(vector3s[idx].z);
                        break;
                    case 4:
                        _mesh.GetUVs(uVId, vector4s);
                        rows.Add(vector4s[idx].x);
                        rows.Add(vector4s[idx].y);
                        rows.Add(vector4s[idx].z);
                        rows.Add(vector4s[idx].w);
                        break;
                }
            }
            return rows.ToArray();
        }

        private MultiColumnHeaderState.Column[] CreateColumns(VertexAttributeDescriptor[] attributes)
        {
            List<MultiColumnHeaderState.Column> columns = new List<MultiColumnHeaderState.Column>();
            columns.Add(CreateColumn("", 40.0f));

            foreach (VertexAttributeDescriptor a in attributes)
            {
                if (a.attribute == VertexAttribute.BlendIndices || a.attribute == VertexAttribute.BlendWeight)
                    continue;
                
                var label = a.attribute.ToString();
                if (label.Contains("TexCoord"))
                    label = label.Replace("TexCoord", "UV");
                else
                    label = label[0].ToString();
                if (a.attribute == VertexAttribute.Color)
                {
                    columns.Add(CreateColumn($"{label} [R]"));
                    columns.Add(CreateColumn($"{label} [G]"));
                    columns.Add(CreateColumn($"{label} [B]"));
                    columns.Add(CreateColumn($"{label} [A]"));
                }
                else
                {
                    columns.Add(CreateColumn($"{label} [X]"));
                    columns.Add(CreateColumn($"{label} [Y]"));
                    if (a.dimension >= 3)
                        columns.Add(CreateColumn($"{label} [Z]"));
                    if (a.dimension == 4)
                        columns.Add(CreateColumn($"{label} [W]"));
                }
            }

            return columns.ToArray();
        }

        private MultiColumnHeaderState.Column CreateColumn(string header, float minWidth = 74.0f)
        {
            MultiColumnHeaderState.Column column = new MultiColumnHeaderState.Column();
            column.headerContent = new GUIContent(header);
            column.minWidth = minWidth;
            column.width = column.minWidth;
            column.allowToggleVisibility = false;
            column.canSort = false;

            return column;
        }
    }
}