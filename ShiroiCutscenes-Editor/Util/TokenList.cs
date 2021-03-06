﻿using System.Collections.Generic;
using System.Linq;
using Shiroi.Cutscenes.Editor.Windows;
using Shiroi.Cutscenes.Tokens;
using UnityEditor;
using UnityEngine;

namespace Shiroi.Cutscenes.Editor.Util {
    public sealed class TokenStateTuple {
        public readonly EventType Type;
        public readonly TokenList.TokenListState State;

        public TokenStateTuple(EventType type, TokenList.TokenListState state) {
            Type = type;
            State = state;
        }
    }

    public class TokenList {
        //Label, Delete
        //Name
        private const byte TotalExtraFields = 2;
        private readonly CutsceneEditor editor;

        public const float FooterHeight = 14F;

        //How further down from the start of the selected token box we are dragging 
        private float dragOffset;
        private readonly SlideGroup slideGroup = new SlideGroup();
        private float draggedY;
        private bool dragging;
        private readonly List<int> nonDragTargetIndices = new List<int>();
        private readonly TokenStateTuple[] states;

        public delegate void TokenListState(Rect listRect, Event e);

        public TokenList(CutsceneEditor editor) {
            this.editor = editor;
            states = new[] {
                new TokenStateTuple(EventType.MouseDown, OnMouseDown),
                new TokenStateTuple(EventType.MouseUp, OnMouseUp),
                new TokenStateTuple(EventType.MouseDrag, OnMouseDrag),
                new TokenStateTuple(EventType.KeyDown, OnKeyDown),
            };
        }

        public Cutscene Cutscene {
            get {
                return editor.Cutscene;
            }
        }

        public int index;


        private static Rect GetContentRect(Rect rect) {
            var rect1 = rect;
            rect1.xMin += 20f;
            rect1.xMax -= 6f;
            return rect1;
        }

        private float GetTokenHeight(int tokenIndex) {
            if (tokenIndex >= Cutscene.Count) {
                Debug.LogWarningFormat(
                    "[ShiroiCutscenes] Token index '{0}' is out of range in cutscenes {1}!",
                    tokenIndex,
                    Cutscene.name);
                return ShiroiStyles.SingleLineHeight;
            }

            var token = Cutscene[tokenIndex];

            return MappedToken.For(token).Height + ShiroiStyles.SingleLineHeight * TotalExtraFields;
        }

        private float GetElementYOffset(int tokenIndex, int skipIndex = -1) {
            var num = 0.0f;
            for (var i = 0; i < tokenIndex; ++i) {
                if (i != skipIndex) {
                    num += GetTokenHeight(i);
                }
            }

            return num;
        }

        public int Count {
            get {
                return Cutscene.Count;
            }
        }

        public bool HasSelected {
            get {
                return index >= 0;
            }
        }

        private float GetListElementHeight() {
            return (float) (GetElementYOffset(Count - 1) + (double) GetTokenHeight(Count - 1) + 7.0);
        }


        private void DrawNormal(Rect listRect) {
            var rect1 = listRect;
            for (var i = 0; i < Count; ++i) {
                var isSelected = i == index;
                var flag2 = i == index; //&& HasKeyboardControl();
                rect1.height = GetTokenHeight(i);
                rect1.y = listRect.y + GetElementYOffset(i);
                DrawBackground(rect1, i, isSelected, flag2);
                DrawDraggingHandle(rect1);
                var contentRect = GetContentRect(rect1);
                DrawToken(contentRect, i, isSelected, flag2);
            }
        }

        private void DrawDragging(Rect listRect) {
            var rowIndex = GetDraggedRowIndex();
            nonDragTargetIndices.Clear();
            for (var i = 0; i < Count; ++i) {
                if (i != index) {
                    nonDragTargetIndices.Add(i);
                }
            }

            nonDragTargetIndices.Insert(rowIndex, -1);
            var rect1 = listRect;

            var pastBeingDragged = false;
            //Draw not selected elements
            for (var i = 0; i < nonDragTargetIndices.Count; ++i) {
                var i2 = nonDragTargetIndices[i];
                if (i2 != -1) {
                    rect1.height = GetTokenHeight(i);

                    rect1.y = listRect.y + GetElementYOffset(i2, index);
                    if (pastBeingDragged) {
                        rect1.y += GetTokenHeight(index);
                    }

                    rect1 = slideGroup.GetRect(editor, i2, rect1);
                    rect1.height = GetTokenHeight(i2);
                    DrawBackground(rect1, i2, false, false);
                    DrawDraggingHandle(rect1);
                    var contentRect = GetContentRect(rect1);
                    DrawToken(contentRect, nonDragTargetIndices[i], false, false);
                } else {
                    pastBeingDragged = true;
                }
            }

            //Draw selected token
            rect1.y = draggedY - dragOffset + listRect.y;
            //rect1.height = GetTokenHeight(index);
            DrawBackground(rect1, index, true, true);
            DrawDraggingHandle(rect1);
            var contentRect1 = GetContentRect(rect1);
            DrawToken(contentRect1, index, true, true);
        }

        private void DrawToken(Rect rect, int i, bool isactive, bool isfocused) {
            var token = Cutscene[i];
            var mappedToken = MappedToken.For(token);
            bool changed;
            var content = new GUIContent(string.Format("#{0} - {1}", i, mappedToken.Label));
            var headerRect = rect.GetLine(0);
            var buttonRect = rect.SubRectFarRight(ContextWindow.RemoveTokenContent);
            EditorGUI.LabelField(headerRect, content, ShiroiStyles.Bold);
            var initColor = GUI.backgroundColor;
            GUI.backgroundColor = ShiroiStyles.ErrorBackgroundColor;
            var remove = GUI.Button(buttonRect, ContextWindow.RemoveTokenContent);
            GUI.backgroundColor = initColor;
            token.name = EditorGUI.TextField(rect.GetLine(1), "Token Name", token.name);
            var subRect = rect;
            subRect.yMin += TotalExtraFields * ShiroiStyles.SingleLineHeight;
            mappedToken.DrawFields(editor, subRect, i, token, Cutscene, editor.Player, content, out changed);

            if (remove) {
                editor.RemoveToken(i);
            }

            if (changed || remove) {
                EditorUtility.SetDirty(Cutscene);
            }

            if (!changed) {
                return;
            }

            var l = token as ITokenChangedListener;
            if (l != null) {
                l.OnChanged(Cutscene);
            }
        }

        private void DrawDraggingHandle(Rect rect) {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            GUIStyle draggingHandle = "RL DragHandle";
            draggingHandle.Draw(
                new Rect(rect.x + 5f, rect.y + 7f, 10f, rect.height - (rect.height - 7f)),
                false,
                false,
                false,
                false);
        }

        private void DrawBackground(Rect rect, int index, bool isactive, bool isfocused) {
            if (index == -1) {
                return;
            }

            var m = MappedToken.For(Cutscene[index]);
            var initColor = GUI.backgroundColor;
            GUI.backgroundColor = isfocused ? m.SelectedColor : m.Color;
            GUI.Box(rect, GUIContent.none);
            GUI.backgroundColor = initColor;
        }


        private TokenListState GetState(EventType type) {
            var found = states.FirstOrDefault(tuple => tuple.Type == type);
            return found != null ? found.State : null;
        }

        private void OnMouseDown(Rect listRect, Event e) {
            if (listRect.Contains(Event.current.mousePosition) && Event.current.button == 0) {
                index = GetRowIndex(Event.current.mousePosition.y - listRect.y);
                dragOffset = Event.current.mousePosition.y - listRect.y - GetElementYOffset(index);
                UpdateDraggedY(listRect);
                slideGroup.Reset();
                e.Use();
            }
        }

        private void OnMouseUp(Rect listRect, Event e) {
            e.Use();
            dragging = false;
            var rowIndex = GetDraggedRowIndex();
            Cutscene.Swap(index, rowIndex);
            index = rowIndex;
        }

        private void OnMouseDrag(Rect listRect, Event e) {
            dragging = true;
            UpdateDraggedY(listRect);
            e.Use();
        }

        private void OnKeyDown(Rect listRect, Event e) {
            if (e.keyCode == KeyCode.DownArrow) {
                index++;
                e.Use();
            }

            if (e.keyCode == KeyCode.UpArrow) {
                index--;
                e.Use();
            }

            if (e.keyCode == KeyCode.Escape) {
                GUIUtility.hotControl = 0;
                dragging = false;
                e.Use();
            }

            index = Mathf.Clamp(index, 0, Cutscene.Count - 1);
        }

        private void DoDraggingAndSelection(Rect listRect) {
            var e = Event.current;
            var state = GetState(e.type);
            if (state != null) {
                state(listRect, e);
            }
        }


        private void UpdateDraggedY(Rect listRect) {
            draggedY = Mathf.Clamp(
                Event.current.mousePosition.y - listRect.y,
                dragOffset,
                listRect.height - (GetTokenHeight(index) - dragOffset));
        }

        private int GetDraggedRowIndex() {
            return GetRowIndex(draggedY);
        }

        private int GetRowIndex(float localY) {
            var num1 = 0.0f;
            for (var i = 0; i < Count; ++i) {
                var num2 = GetTokenHeight(i);
                var num3 = num1 + num2;
                if (localY >= (double) num1 && localY < (double) num3) {
                    return i;
                }

                num1 += num2;
            }

            return Count - 1;
        }

        public void Draw() {
            var rect = GUILayoutUtility.GetRect(10f, GetListElementHeight(), GUILayout.ExpandWidth(true));
            var footerRect = GUILayoutUtility.GetRect(10f, FooterHeight, GUILayout.ExpandWidth(true));
            if (Cutscene.IsEmpty) {
                return;
            }

            if (dragging && Event.current.type == EventType.Repaint) {
                DrawDragging(rect);
            } else {
                DrawNormal(rect);
            }

            DrawFooter(footerRect);
            DoDraggingAndSelection(rect);
        }

        private void DrawFooter(Rect rect) {
            var position = new Rect(rect.xMax - 29f, rect.y - 3f, 25f, 13f);

            var iconToolbarMinus = EditorGUIUtility.IconContent("Toolbar Minus", "|Remove selection from list");
            var preButton = (GUIStyle) "RL FooterButton";
            using (new EditorGUI.DisabledScope(index < 0 || index >= Count)) {
                if (GUI.Button(position, iconToolbarMinus, preButton)) {
                    editor.Cutscene.RemoveToken(index);
                }
            }
        }
    }
}