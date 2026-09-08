using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    internal enum ContentMakerMapPage
    {
        RegionDetails,
        NewRegion,
        RoomEdit,
        NewRoom,
        RoomDuplicate,
        RoomScene,
        DeleteRoom,
        DeleteRegion
    }

    internal sealed class ContentMakerContext
    {
        public string RegionPath;
        public RoomDefinition Room;
        public DialogueData Dialogue;
        public Object SelectedAsset;

        private ContentMakerMapPage _mapPage = ContentMakerMapPage.RoomEdit;
        public ContentMakerMapPage MapPage
        {
            get => _mapPage;
            set
            {
                if (_mapPage == value) return;
                _mapPage = value;
                MapPageChanged?.Invoke();
            }
        }

        public Action<Object> SelectionChanged;
        public Action MapPageChanged;
        public Action RefreshRequested;
        public Action<string, MessageType> StatusChanged;

        public void Select(Object target)
        {
            SelectedAsset = target;
            if (target is RoomDefinition room) Room = room;
            if (target is DialogueData dialogue) Dialogue = dialogue;
            SelectionChanged?.Invoke(target);
        }

        public void RefreshAssets() => RefreshRequested?.Invoke();
        public void Report(string message, MessageType type = MessageType.Info) => StatusChanged?.Invoke(message, type);
    }
}
