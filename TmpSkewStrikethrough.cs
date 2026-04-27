using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ThirdParty.TMPro
{
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TmpSkewStrikethrough : MonoBehaviour
    {
        [SerializeField] private float _angle = 35f;
        [SerializeField] private float _verticalOffset = 0f;
        [SerializeField] private float _maxLineHeight = 8f;
        [SerializeField] private float _groupYTolerance = 4f;
        [SerializeField] private float _minLineWidth = 0.01f;
        [SerializeField] private float _maxGroupXGap = 6f;
        [SerializeField] private float _maxGroupYDelta = 8f;
        [SerializeField] private bool _reparentSprites = true;

        private readonly List<LineQuad> _lineQuads = new();
        private readonly List<List<int>> _groups = new();
        private readonly List<TMP_SubMeshUI> _temp = new();
        private TMP_Text _text;

        private struct LineQuad
        {
            public int MeshIndex;
            public int VertexIndex;
            public float MinX;
            public float MaxX;
            public float CenterY;
        }

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            _text.OnPreRenderText += OnPreRenderText;
            _text.ForceMeshUpdate();
        }

        private void OnDisable()
        {
            if (_text != null)
            {
                _text.OnPreRenderText -= OnPreRenderText;
                _text.ForceMeshUpdate();
            }
        }

        private void OnPreRenderText(TMP_TextInfo textInfo)
        {
            _lineQuads.Clear();
            _groups.Clear();

            CollectLineQuads(textInfo);
            BuildGroups();
            SkewGroups(textInfo);

            if (_reparentSprites)
            {
                _text.GetComponentsInChildren(true, _temp);

                foreach (TMP_SubMeshUI subMesh in _temp)
                {
                    if (subMesh.spriteAsset != null)
                    {
                        subMesh.transform.SetParent(transform.parent);
                        subMesh.transform.SetSiblingIndex(transform.GetSiblingIndex());
                    }
                }
            }
        }

        private void CollectLineQuads(TMP_TextInfo textInfo)
        {
            for (int meshIndex = 0; meshIndex < textInfo.meshInfo.Length; meshIndex++)
            {
                TMP_MeshInfo meshInfo = textInfo.meshInfo[meshIndex];
                Vector3[] vertices = meshInfo.vertices;

                int quadCount = meshInfo.vertexCount / 4;

                for (int quadIndex = 0; quadIndex < quadCount; quadIndex++)
                {
                    int vertexIndex = quadIndex * 4;

                    Vector3 v0 = vertices[vertexIndex + 0];
                    Vector3 v1 = vertices[vertexIndex + 1];
                    Vector3 v2 = vertices[vertexIndex + 2];
                    Vector3 v3 = vertices[vertexIndex + 3];

                    float minX = Mathf.Min(v0.x, v1.x, v2.x, v3.x);
                    float maxX = Mathf.Max(v0.x, v1.x, v2.x, v3.x);
                    float minY = Mathf.Min(v0.y, v1.y, v2.y, v3.y);
                    float maxY = Mathf.Max(v0.y, v1.y, v2.y, v3.y);

                    float width = maxX - minX;
                    float height = maxY - minY;

                    if (height <= 0f)
                    {
                        continue;
                    }

                    if (height > _maxLineHeight)
                    {
                        continue;
                    }

                    if (width < _minLineWidth)
                    {
                        continue;
                    }

                    LineQuad lineQuad = new LineQuad();
                    lineQuad.MeshIndex = meshIndex;
                    lineQuad.VertexIndex = vertexIndex;
                    lineQuad.MinX = minX;
                    lineQuad.MaxX = maxX;
                    lineQuad.CenterY = (minY + maxY) * 0.5f;

                    _lineQuads.Add(lineQuad);
                }
            }
        }

        private void BuildGroups()
        {
            _lineQuads.Sort(CompareLineQuads);

            for (int i = 0; i < _lineQuads.Count; i++)
            {
                bool added = false;

                for (int groupIndex = 0; groupIndex < _groups.Count; groupIndex++)
                {
                    List<int> group = _groups[groupIndex];

                    float groupMinX = float.MaxValue;
                    float groupMaxX = float.MinValue;
                    float groupY = 0f;

                    for (int j = 0; j < group.Count; j++)
                    {
                        LineQuad quad = _lineQuads[group[j]];

                        groupMinX = Mathf.Min(groupMinX, quad.MinX);
                        groupMaxX = Mathf.Max(groupMaxX, quad.MaxX);
                        groupY += quad.CenterY;
                    }

                    groupY /= group.Count;

                    LineQuad current = _lineQuads[i];

                    bool closeByY = Mathf.Abs(current.CenterY - groupY) <= _maxGroupYDelta;

                    bool touchesByX = current.MinX <= groupMaxX + _maxGroupXGap &&
                                      current.MaxX >= groupMinX - _maxGroupXGap;

                    if (closeByY && touchesByX)
                    {
                        group.Add(i);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    List<int> group = new List<int>();
                    group.Add(i);
                    _groups.Add(group);
                }
            }
        }

        private static int CompareLineQuads(LineQuad a, LineQuad b)
        {
            int yCompare = a.CenterY.CompareTo(b.CenterY);

            if (yCompare != 0)
            {
                return yCompare;
            }

            return a.MinX.CompareTo(b.MinX);
        }

        private void SkewGroups(TMP_TextInfo textInfo)
        {
            float tan = Mathf.Tan(_angle * Mathf.Deg2Rad);

            for (int groupIndex = 0; groupIndex < _groups.Count; groupIndex++)
            {
                List<int> group = _groups[groupIndex];

                float minX = float.MaxValue;
                float maxX = float.MinValue;
                float centerY = 0f;

                for (int i = 0; i < group.Count; i++)
                {
                    LineQuad lineQuad = _lineQuads[group[i]];

                    minX = Mathf.Min(minX, lineQuad.MinX);
                    maxX = Mathf.Max(maxX, lineQuad.MaxX);
                    centerY += lineQuad.CenterY;
                }

                centerY = centerY / group.Count + _verticalOffset;

                float centerX = (minX + maxX) * 0.5f;

                for (int i = 0; i < group.Count; i++)
                {
                    LineQuad lineQuad = _lineQuads[group[i]];
                    Vector3[] vertices = textInfo.meshInfo[lineQuad.MeshIndex].vertices;

                    for (int v = 0; v < 4; v++)
                    {
                        int vertexIndex = lineQuad.VertexIndex + v;

                        Vector3 position = vertices[vertexIndex];
                        position.y = centerY + (position.y - lineQuad.CenterY) + (position.x - centerX) * tan;

                        vertices[vertexIndex] = position;
                    }
                }
            }

            for (int meshIndex = 0; meshIndex < textInfo.meshInfo.Length; meshIndex++)
            {
                textInfo.meshInfo[meshIndex].mesh.vertices = textInfo.meshInfo[meshIndex].vertices;
            }
        }

        private void OnValidate()
        {
            GetComponent<TMP_Text>().ForceMeshUpdate();
        }
    }
}
