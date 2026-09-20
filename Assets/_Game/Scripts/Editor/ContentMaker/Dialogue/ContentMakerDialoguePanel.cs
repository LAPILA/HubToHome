using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HubToHome.EditorTools.ContentMaker
{
    internal sealed class ContentMakerDialoguePanel : IDisposable
    {
        private const string SpeakersFolder = "Assets/_Game/Content/Dialogue/Data/Speakers";
        private const string CsvHelp = "한 파일 = 한 DialogueData. document 행은 표시 방식, node 행은 대사, 바로 뒤 choice 행은 그 대사의 선택지입니다. " +
            "node/choice 번호는 0부터 연속 순서입니다. 열 제목과 schema=1은 유지하세요. " +
            "화자/다음 대화는 GUID와 경로로 연결하며, 새 참조는 GUID를 비우고 Assets/ 경로를 입력할 수 있습니다. " +
            "엑셀에서는 'CSV UTF-8(쉼표로 분리)'로 저장하세요. 셀 내부 줄바꿈·따옴표·쉼표를 지원합니다. " +
            "엑셀에서 파일을 바로 더블클릭하면 0012, 날짜처럼 보이는 문장, 긴 숫자가 자동 변환될 수 있습니다. " +
            "데이터 > 텍스트/CSV에서 가져오기를 사용하고 모든 열을 '텍스트' 형식으로 지정하세요. " +
            "수식으로 오인될 수 있는 본문 앞의 보호용 작은따옴표는 가져올 때 복원됩니다. 자동 동기화/XLSX는 지원하지 않습니다.";

        private DialogueData _activeDialogue;
        private static readonly string[] WorkPages = { "대사", "화자·표정", "조건·NPC", "CSV·번역", "문서" };
        private static readonly string[] TransferPages = { "대본 CSV", "번역 준비" };
        private readonly ContentMakerDialogueRulesPanel _rulesPanel = new ContentMakerDialogueRulesPanel();
        private int _workPage;
        private int _transferPage;
        private int _nodeIndex;
        private string _newName = "Dialogue_New";
        private string _rename;
        private string[] _nodeLabels = Array.Empty<string>();
        private bool _labelsDirty = true;
        private bool _showAdvanced;
        private bool _showCsvHelp;
        private SpeakerData _speaker;
        private PropertyTree _speakerTree;
        private string _newSpeakerName = "새 인물";
        private string _newSpeakerId = "speaker_new";
        private ContentMakerDialogueImport _pendingImport;
        private DialogueData _pendingTarget;
        private string _pendingSource;
        private string _pendingBaseline;
        private string _pendingCsv;
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _pendingWarnings = new List<string>();

        public ContentMakerDialoguePanel()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            _labelsDirty = true;
            _warnings.Clear();
            ClearPendingImport();
        }

        public void OnGUI(ContentMakerContext context)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                ContentMakerGUI.Header("대사 제작", "왼쪽에서 대화를 선택한 뒤 필요한 작업을 고르세요. 대본과 NPC 배치를 따로 관리합니다.");
                DialogueData selected = context.Dialogue;
                if (_activeDialogue != selected)
                {
                    _activeDialogue = selected;
                    _nodeIndex = 0;
                    _rename = selected != null ? selected.name : string.Empty;
                    _labelsDirty = true;
                    ClearPendingImport();
                    _warnings.Clear();
                }
                using (ContentMakerGUI.Card())
                {
                    EditorGUILayout.LabelField(selected != null ? selected.name : "대화를 선택하거나 새로 만드세요", ContentMakerGUI.SectionTitle);
                    if (selected != null)
                    {
                        EditorGUILayout.LabelField($"{selected.Nodes?.Count ?? 0}줄 · 공유 중인 NPC에도 수정 내용이 적용됩니다.", ContentMakerGUI.Muted);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (ContentMakerGUI.PrimaryButton("이 대화 저장"))
                            {
                                ContentMakerAssetUtility.Save(selected);
                                context.Report("선택한 대화만 저장했습니다.");
                            }
                            if (ContentMakerGUI.SecondaryButton("Project에서 찾기")) EditorGUIUtility.PingObject(selected);
                        }
                    }
                    int nextPage = GUILayout.Toolbar(_workPage, WorkPages, GUILayout.Height(28));
                    if (nextPage != _workPage && nextPage == 0) _labelsDirty = true;
                    _workPage = nextPage;
                }
                switch (_workPage)
                {
                    case 1:
                        DrawSpeaker(context, selected);
                        break;
                    case 2:
                        _rulesPanel.OnGUI(context);
                        break;
                    case 3:
                    {
                        if (selected == null) { DrawEmptyDocument(context); break; }
                        _transferPage = GUILayout.Toolbar(_transferPage, TransferPages, GUILayout.Height(26));
                        if (_transferPage == 0) DrawCsv(context, selected);
                        else DrawTranslation(context, selected);
                        break;
                    }
                    case 4:
                        using (ContentMakerGUI.Card()) DrawCreate(context);
                        if (selected != null)
                            using (ContentMakerGUI.Card()) DrawDocument(context, selected);
                        break;
                    default:
                        if (selected == null) DrawEmptyDocument(context);
                        else using (ContentMakerGUI.Card()) DrawNodes(context, selected);
                        break;
                }
            }
        }

        public void ShowCreate() => _workPage = 4;

        private void DrawEmptyDocument(ContentMakerContext context)
        {
            DrawGettingStarted();
            using (ContentMakerGUI.Card()) DrawCreate(context);
        }

        private static void DrawGettingStarted()
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("처음 만드는 대화 · 3단계", ContentMakerGUI.SectionTitle);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("1. 대화 문서 만들기", ContentMakerGUI.Body);
                EditorGUILayout.LabelField("왼쪽 챕터를 선택하고 아래에서 대화 이름을 입력합니다. 한 문서에 여러 줄의 대사가 들어갑니다.", ContentMakerGUI.Muted);
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("2. 대사 · 화자 · 표정 편집", ContentMakerGUI.Body);
                EditorGUILayout.LabelField("본문을 입력하고 '+ 대사'로 다음 줄을 추가합니다. 새 인물은 '화자·표정' 작업에서 등록합니다.", ContentMakerGUI.Muted);
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("3. 저장하고 NPC에 연결", ContentMakerGUI.Body);
                EditorGUILayout.LabelField("'이 대화 저장' 후 '마커 · NPC' 탭에서 NPC를 만들며 '대사 탭에서 선택한 대화 연결'을 누릅니다.", ContentMakerGUI.Muted);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("처음에는 이 창에서 바로 작성하면 됩니다. 엑셀용 CSV는 긴 대본을 외부에서 편집할 때만 사용하는 선택 기능입니다.", ContentMakerGUI.Muted);
            }
        }

        private void DrawCreate(ContentMakerContext context)
        {
            EditorGUILayout.LabelField("새 대화 문서 만들기", ContentMakerGUI.SectionTitle);
            using (new EditorGUILayout.HorizontalScope())
            {
                _newName = EditorGUILayout.TextField(new GUIContent("새 대화 이름", "한글 이름도 가능합니다. 파일명만 변경하며 기존 자산은 덮어쓰지 않습니다."), _newName);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(context.RegionPath) || string.IsNullOrWhiteSpace(_newName)))
                    if (ContentMakerGUI.PrimaryButton("대화 만들기", GUILayout.Width(112)))
                        Run(context, () => CreateDialogue(context));
            }
            if (!string.IsNullOrEmpty(context.RegionPath))
                EditorGUILayout.LabelField("저장 위치 · " + context.RegionPath + "/Data/Dialogue", ContentMakerGUI.Muted);
            else
                EditorGUILayout.LabelField("먼저 왼쪽 챕터 목록에서 저장할 챕터를 선택하세요.", ContentMakerGUI.Muted);
        }

        private void CreateDialogue(ContentMakerContext context)
        {
            string path = ContentMakerAssetUtility.UniqueAssetPath(context.RegionPath + "/Data/Dialogue", _newName, ".asset");
            DialogueData dialogue = ScriptableObject.CreateInstance<DialogueData>();
            dialogue.name = Path.GetFileNameWithoutExtension(path);
            dialogue.Nodes = new List<DialogueNode> { new DialogueNode() };
            AssetDatabase.CreateAsset(dialogue, path);
            ContentMakerAssetUtility.Save(dialogue);
            _workPage = 0;
            context.Select(dialogue);
            context.RefreshAssets();
            context.Report("대화를 만들었습니다. 화자와 첫 대사를 입력하세요.");
        }

        private void DrawDocument(ContentMakerContext context, DialogueData dialogue)
        {
            EditorGUILayout.LabelField("선택한 대화의 문서 설정", ContentMakerGUI.SectionTitle);
            EditorGUI.BeginChangeCheck();
            int style = EditorGUILayout.Popup("대화창 종류", (int)dialogue.Style, new[] { "오버월드 대화창", "시네마틱 대화창" });
            if (EditorGUI.EndChangeCheck())
                Change(dialogue, "대화창 종류 변경", () => dialogue.Style = (DialogueStyle)style);
            EditorGUILayout.LabelField("이 대화를 공유하는 NPC에도 수정 내용이 함께 적용됩니다.", ContentMakerGUI.Muted);
            EditorGUILayout.LabelField("작성 → 이 대화 저장 → 마커 · NPC에서 연결. 프리팹 안에 대사를 다시 쓸 필요는 없습니다.", ContentMakerGUI.Muted);
            using (new EditorGUILayout.HorizontalScope())
            {
                _rename = EditorGUILayout.TextField("파일 이름", _rename);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_rename) || _rename == dialogue.name))
                    if (ContentMakerGUI.SecondaryButton("이름 변경", GUILayout.Width(90)))
                        Run(context, () =>
                        {
                            string safeName = ContentMakerAssetUtility.MakeFileName(_rename);
                            ContentMakerDialogueRenameGuard.EnsureCanRename(dialogue);
                            string error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(dialogue), safeName);
                            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                            _rename = dialogue.name;
                            context.RefreshAssets();
                            context.Report("이름을 변경했습니다. Unity 자산 참조는 유지됩니다. 외부로 내보낸 CSV는 필요하면 다시 내보내세요.");
                        });
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(context.RegionPath)))
                if (ContentMakerGUI.SecondaryButton("현재 지역에 복제"))
                    Run(context, () => DuplicateDialogue(context, dialogue));
        }

        private void DuplicateDialogue(ContentMakerContext context, DialogueData source)
        {
            string path = ContentMakerAssetUtility.UniqueAssetPath(context.RegionPath + "/Data/Dialogue", source.name + "_Copy", ".asset");
            DialogueData copy = Object.Instantiate(source);
            copy.name = Path.GetFileNameWithoutExtension(path);
            if (copy.Nodes != null)
                foreach (DialogueNode node in copy.Nodes)
                    if (node?.Choices != null)
                        foreach (ChoiceData choice in node.Choices)
                            if (choice != null && choice.NextDialogue == source) choice.NextDialogue = copy;
            AssetDatabase.CreateAsset(copy, path);
            ContentMakerAssetUtility.Save(copy);
            _workPage = 0;
            context.Select(copy);
            context.RefreshAssets();
            context.Report("대화를 복제했습니다. 화자·다른 대화와 번역 키를 공유합니다. 다른 문장으로 쓸 줄은 번역 키를 비우고 새 키를 생성하세요.", MessageType.Warning);
        }

        private void DrawNodes(ContentMakerContext context, DialogueData dialogue)
        {
            EditorGUILayout.LabelField("대사 편집", ContentMakerGUI.SectionTitle);
            if (dialogue.Nodes == null)
            {
                EditorGUILayout.HelpBox("대사 목록이 없습니다.", MessageType.Warning);
                if (GUILayout.Button("빈 대사 목록 준비"))
                    Change(dialogue, "대사 목록 준비", () => dialogue.Nodes = new List<DialogueNode>());
                return;
            }
            if (_labelsDirty || _nodeLabels.Length != dialogue.Nodes.Count || Event.current.type == EventType.Layout && GUI.changed)
                RebuildLabels(dialogue);
            _nodeIndex = Mathf.Clamp(_nodeIndex, 0, Mathf.Max(0, dialogue.Nodes.Count - 1));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("대사 순서", GUILayout.Width(65));
                using (new EditorGUI.DisabledScope(dialogue.Nodes.Count == 0))
                    _nodeIndex = EditorGUILayout.Popup(_nodeIndex, _nodeLabels);
                if (ContentMakerGUI.PrimaryButton("+ 대사", GUILayout.Width(88)))
                {
                    Change(dialogue, "대사 추가", () =>
                    {
                        int insert = dialogue.Nodes.Count == 0 ? 0 : _nodeIndex + 1;
                        SpeakerData previous = dialogue.Nodes.Count > 0 ? dialogue.Nodes[_nodeIndex]?.Speaker : null;
                        dialogue.Nodes.Insert(insert, new DialogueNode { Speaker = previous });
                        _nodeIndex = insert;
                    });
                    return;
                }
            }
            if (dialogue.Nodes.Count == 0)
            {
                EditorGUILayout.HelpBox("'+ 대사'로 첫 줄을 추가하세요.", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_nodeIndex <= 0))
                    if (ContentMakerGUI.SecondaryButton("위로 이동")) { MoveNode(dialogue, -1); return; }
                using (new EditorGUI.DisabledScope(_nodeIndex >= dialogue.Nodes.Count - 1))
                    if (ContentMakerGUI.SecondaryButton("아래로 이동")) { MoveNode(dialogue, 1); return; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (ContentMakerGUI.SecondaryButton("이 대사 복제"))
                {
                    Change(dialogue, "대사 복제", () =>
                    {
                        dialogue.Nodes.Insert(_nodeIndex + 1, CloneNode(dialogue.Nodes[_nodeIndex]));
                        _nodeIndex++;
                    });
                    context.Report("대사를 복제했습니다. 번역 키도 공유하므로 다른 문장으로 쓸 경우 키를 비우고 새 키를 생성하세요.", MessageType.Warning);
                    return;
                }
                if (ContentMakerGUI.SecondaryButton("삭제"))
                {
                    Change(dialogue, "대사 삭제", () => dialogue.Nodes.RemoveAt(_nodeIndex));
                    return;
                }
            }

            DialogueNode node = dialogue.Nodes[_nodeIndex];
            if (node == null)
            {
                EditorGUILayout.HelpBox("빈 대사 데이터입니다. 삭제하거나 새 대사로 교체하세요.", MessageType.Warning);
                if (GUILayout.Button("새 대사로 교체"))
                    Change(dialogue, "빈 대사 복구", () => dialogue.Nodes[_nodeIndex] = new DialogueNode());
                return;
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"{_nodeIndex + 1} / {dialogue.Nodes.Count}번째 대사", ContentMakerGUI.Muted);
            EditorGUI.BeginChangeCheck();
            SpeakerData speaker = (SpeakerData)EditorGUILayout.ObjectField(new GUIContent("말하는 인물", "비워두면 화자 없는 내레이션입니다."), node.Speaker, typeof(SpeakerData), false);
            EmotionType emotion = (EmotionType)EditorGUILayout.EnumPopup("표정", node.Emotion);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("대사 내용 (여러 줄 가능)", ContentMakerGUI.Body);
            string text = EditorGUILayout.TextArea(node.DefaultText ?? string.Empty, ContentMakerGUI.TextArea, GUILayout.MinHeight(120));
            if (!string.IsNullOrWhiteSpace(node.LocalizationKey))
                EditorGUILayout.HelpBox("번역 키: " + node.LocalizationKey
                    + "\n번역표에 이 키가 있으면 위 본문보다 번역표 문장이 우선합니다. 복제한 줄을 다른 문장으로 쓰려면 아래 ‘번역 키 / 고급 이벤트’에서 키를 비우고 ‘CSV·번역 → 번역 준비’에서 새 키를 생성하세요. 기존 키를 공유하는 다른 대사는 바뀌지 않습니다.", MessageType.Warning);
            EditorGUILayout.Space(4);
            bool isChoice = EditorGUILayout.Toggle("선택지 사용", node.IsChoiceNode);
            if (EditorGUI.EndChangeCheck())
                Change(dialogue, "대사 편집", () =>
                {
                    node.Speaker = speaker;
                    node.Emotion = emotion;
                    node.DefaultText = text;
                    node.IsChoiceNode = isChoice;
                });
            DrawPortrait(node.Speaker, node.Emotion);

            _showAdvanced = ContentMakerGUI.Foldout(_showAdvanced, "번역 키 / 고급 이벤트");
            if (_showAdvanced)
            {
                EditorGUI.BeginChangeCheck();
                string key = EditorGUILayout.TextField("본문 번역 키", node.LocalizationKey ?? string.Empty);
                string eventId = EditorGUILayout.TextField("기존 이벤트 ID", node.EventTriggerID ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                    Change(dialogue, "대사 고급 설정", () => { node.LocalizationKey = key; node.EventTriggerID = eventId; });
                EditorGUILayout.HelpBox("번역 키가 등록되어 있으면 번역표 내용이 본문보다 우선합니다. 이벤트 ID에는 개발자가 등록한 값만 사용하세요. 이동·카메라 등 복잡한 연출은 기존 시퀀스 메이커에서 구성합니다.", MessageType.None);
            }
            if (node.IsChoiceNode)
                DrawChoices(dialogue, node);
            EditorGUILayout.Space(6);
            if (ContentMakerGUI.SecondaryButton("이 대화 연결 확인"))
            {
                _warnings.Clear();
                _warnings.AddRange(ContentMakerDialogueCsv.GetWarnings(dialogue, dialogue.Nodes));
                context.Report(_warnings.Count == 0 ? "대사/선택지 기본 검사를 통과했습니다. 플레이 실행 결과는 별도 확인하세요." : "확인할 대사 설정 " + _warnings.Count + "건이 있습니다.", _warnings.Count == 0 ? MessageType.Info : MessageType.Warning);
            }
            foreach (string warning in _warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private void DrawChoices(DialogueData dialogue, DialogueNode node)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("선택지", ContentMakerGUI.SectionTitle);
            EditorGUILayout.LabelField("다음 대화를 비우면 선택 시 종료합니다. 전투 시작은 전투 NPC의 연결이 필요합니다. 표시할 문장을 그대로 입력하세요.", ContentMakerGUI.Muted);
            if (node.Choices == null)
            {
                if (GUILayout.Button("선택지 목록 준비"))
                    Change(dialogue, "선택지 목록 준비", () => node.Choices = new List<ChoiceData>());
                return;
            }
            for (int index = 0; index < node.Choices.Count; index++)
            {
                int choiceIndex = index;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("선택지 " + (index + 1), ContentMakerGUI.SectionTitle);
                        using (new EditorGUI.DisabledScope(index == 0))
                            if (GUILayout.Button("↑", GUILayout.Width(28)))
                            {
                                Change(dialogue, "선택지 순서 변경", () =>
                                {
                                    ChoiceData swap = node.Choices[choiceIndex - 1];
                                    node.Choices[choiceIndex - 1] = node.Choices[choiceIndex];
                                    node.Choices[choiceIndex] = swap;
                                });
                                return;
                            }
                        if (GUILayout.Button("삭제", GUILayout.Width(50)))
                        {
                            Change(dialogue, "선택지 삭제", () => node.Choices.RemoveAt(choiceIndex));
                            return;
                        }
                    }
                    ChoiceData choice = node.Choices[index];
                    if (choice == null)
                    {
                        if (GUILayout.Button("빈 선택지 복구"))
                            Change(dialogue, "선택지 복구", () => node.Choices[choiceIndex] = new ChoiceData());
                        continue;
                    }
                    EditorGUI.BeginChangeCheck();
                    string text = EditorGUILayout.TextField("표시 문구", choice.ChoiceText ?? string.Empty);
                    DialogueData next = (DialogueData)EditorGUILayout.ObjectField("다음 대화", choice.NextDialogue, typeof(DialogueData), false);
                    string flag = EditorGUILayout.TextField(new GUIContent("완료 플래그", "선택할 때 이 Flag를 1로 설정합니다. 비워두면 변경하지 않습니다."), choice.SetFlagOnSelect ?? string.Empty);
                    bool battle = EditorGUILayout.Toggle("전투 시작", choice.StartBattleEncounter);
                    if (EditorGUI.EndChangeCheck())
                        Change(dialogue, "선택지 편집", () =>
                        {
                            choice.ChoiceText = text;
                            choice.NextDialogue = next;
                            choice.SetFlagOnSelect = flag;
                            choice.StartBattleEncounter = battle;
                        });
                }
            }
            if (ContentMakerGUI.SecondaryButton("+ 선택지 추가"))
                Change(dialogue, "선택지 추가", () => node.Choices.Add(new ChoiceData()));
        }

        private void DrawSpeaker(ContentMakerContext context, DialogueData dialogue)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("화자 · 표정 이미지 관리", ContentMakerGUI.SectionTitle);
                EditorGUILayout.LabelField("공용 화자의 이름·초상화·목소리를 바꾸면 사용하는 모든 대화에 적용됩니다. 표정 이미지는 아래 '초상화' 목록에 연결하세요.", ContentMakerGUI.Muted);
                if (dialogue?.Nodes != null && _nodeIndex >= 0 && _nodeIndex < dialogue.Nodes.Count)
                    EditorGUILayout.HelpBox($"연결 대상: {dialogue.name} / {_nodeIndex + 1}번째 대사\n{ShortText(dialogue.Nodes[_nodeIndex]?.DefaultText, 90)}\n다른 줄에 연결하려면 '대사'에서 먼저 줄을 선택하세요.", MessageType.Info);
                EditorGUILayout.Space(6);
                SpeakerData selected = (SpeakerData)EditorGUILayout.ObjectField("관리할 화자", _speaker, typeof(SpeakerData), false);
                if (dialogue?.Nodes != null && _nodeIndex >= 0 && _nodeIndex < dialogue.Nodes.Count && dialogue.Nodes[_nodeIndex]?.Speaker != null)
                    if (ContentMakerGUI.SecondaryButton("현재 대사의 화자 가져오기")) selected = dialogue.Nodes[_nodeIndex].Speaker;
                if (selected != _speaker) SetSpeaker(selected);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("새 인물 등록", ContentMakerGUI.SectionTitle);
                _newSpeakerName = EditorGUILayout.TextField("새 화자 표시 이름", _newSpeakerName);
                _newSpeakerId = EditorGUILayout.TextField(new GUIContent("새 화자 ID", "저장용 식별자입니다. 다른 인물과 겹치지 않는 영문 ID를 권장합니다."), _newSpeakerId);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newSpeakerName) || string.IsNullOrWhiteSpace(_newSpeakerId)))
                    if (ContentMakerGUI.SecondaryButton("공용 화자 만들기"))
                        Run(context, () => CreateSpeaker(context));
                if (_speaker == null) return;
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("선택한 화자 설정", ContentMakerGUI.SectionTitle);
                if (_speakerTree == null) _speakerTree = PropertyTree.Create(_speaker);
                // Odin owns dictionary serialization and Undo; do not hand-write its serialization bytes.
                _speakerTree.Draw(true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ContentMakerGUI.PrimaryButton("화자 저장"))
                    {
                        ContentMakerAssetUtility.Save(_speaker);
                        context.Report("선택한 공용 화자를 저장했습니다.");
                    }
                    using (new EditorGUI.DisabledScope(dialogue?.Nodes == null || _nodeIndex < 0 || _nodeIndex >= dialogue.Nodes.Count || dialogue.Nodes[_nodeIndex] == null))
                        if (ContentMakerGUI.SecondaryButton("현재 대사에 연결"))
                            Change(dialogue, "대사 화자 연결", () => dialogue.Nodes[_nodeIndex].Speaker = _speaker);
                }
                if (ContentMakerGUI.SecondaryButton("Project에서 찾기")) EditorGUIUtility.PingObject(_speaker);
            }
        }

        private void CreateSpeaker(ContentMakerContext context)
        {
            string id = ContentMakerAssetUtility.MakeId(_newSpeakerId);
            foreach (string guid in AssetDatabase.FindAssets("t:SpeakerData", new[] { "Assets/_Game/Content" }))
            {
                SpeakerData existing = AssetDatabase.LoadAssetAtPath<SpeakerData>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing != null && string.Equals(existing.SpeakerID, id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("이미 사용 중인 화자 ID입니다: " + id);
            }
            string path = ContentMakerAssetUtility.UniqueAssetPath(SpeakersFolder, "Speaker_" + id, ".asset");
            SpeakerData speaker = ScriptableObject.CreateInstance<SpeakerData>();
            speaker.SpeakerID = id;
            speaker.DisplayName = _newSpeakerName.Trim();
            speaker.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(speaker, path);
            ContentMakerAssetUtility.Save(speaker);
            SetSpeaker(speaker);
            context.RefreshAssets();
            context.Report("화자를 만들었습니다. 표정 이미지를 연결하고 '현재 대사에 연결'을 누르세요.");
        }

        private void DrawCsv(ContentMakerContext context, DialogueData dialogue)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("엑셀용 대본 CSV", ContentMakerGUI.SectionTitle);
                EditorGUILayout.HelpBox("가져오기는 이 대화 전체를 교체합니다. 먼저 전체를 검사하고 미리보기를 보여주며, '이 대화에 적용'을 눌러야 변경됩니다. 다른 화자/대화 자산은 수정하지 않습니다.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (ContentMakerGUI.SecondaryButton("현재 대화 내보내기"))
                        Run(context, () => ExportCsv(context, dialogue));
                    if (ContentMakerGUI.SecondaryButton("CSV 검사 / 미리보기"))
                        Run(context, () => ReadCsv(context, dialogue));
                }
                _showCsvHelp = ContentMakerGUI.Foldout(_showCsvHelp, "CSV 작성 방법 / 서식 예시");
                if (_showCsvHelp)
                {
                    EditorGUILayout.LabelField(CsvHelp, ContentMakerGUI.Muted);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("감정: None / Normal / Happy / Sad / Angry / Shocked / Confused", ContentMakerGUI.Muted);
                    EditorGUILayout.LabelField("스타일: Overworld / Cinematic     참/거짓: true / false", ContentMakerGUI.Muted);
                    EditorGUILayout.LabelField("서식은 현재 대화를 내보내면 생성됩니다. node 행 바로 아래 choice 행을 두고, 다른 종류의 열은 비웁니다.", ContentMakerGUI.Muted);
                    if (ContentMakerGUI.SecondaryButton("한 줄 + 선택지 샘플 CSV 저장"))
                        Run(context, () => ExportSample(context));
                }
                if (_pendingImport == null || _pendingTarget != dialogue) return;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("반영 전 미리보기", ContentMakerGUI.SectionTitle);
                    EditorGUILayout.LabelField(Path.GetFileName(_pendingSource), ContentMakerGUI.Muted);
                    EditorGUILayout.LabelField($"기존 {dialogue.Nodes?.Count ?? 0}줄 → 가져온 {_pendingImport.Nodes.Count}줄 / 선택지 {_pendingImport.ChoiceCount}개", ContentMakerGUI.Body);
                    EditorGUILayout.LabelField("대화창: " + _pendingImport.Style, ContentMakerGUI.Muted);
                    int previewCount = Mathf.Min(5, _pendingImport.Nodes.Count);
                    for (int index = 0; index < previewCount; index++)
                    {
                        DialogueNode node = _pendingImport.Nodes[index];
                        string speaker = node.Speaker != null ? node.Speaker.DisplayName : "내레이션";
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField($"{index + 1}. [{speaker} / {node.Emotion}] {ShortText(node.DefaultText, 160)}", ContentMakerGUI.Body);
                    }
                    if (_pendingImport.Nodes.Count > previewCount)
                        EditorGUILayout.LabelField($"외 {_pendingImport.Nodes.Count - previewCount}줄. 적용 전 전체 내용은 원본 CSV를 확인하세요.", ContentMakerGUI.Muted);
                    foreach (string warning in _pendingWarnings)
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                    EditorGUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (ContentMakerGUI.PrimaryButton("이 대화에 적용"))
                            Run(context, () => ApplyCsv(context, dialogue));
                        if (ContentMakerGUI.SecondaryButton("가져오기 취소")) ClearPendingImport();
                    }
                }
            }
        }

        private void DrawTranslation(ContentMakerContext context, DialogueData dialogue)
        {
            using (ContentMakerGUI.Card())
            {
                EditorGUILayout.LabelField("본문 번역 연결 준비", ContentMakerGUI.SectionTitle);
                EditorGUILayout.HelpBox("대본 CSV와 번역표는 서로 다른 파일입니다. 현재 번역 연결 대상은 대사 본문입니다. 선택지 문구와 화자 이름은 아직 번역 키를 사용하지 않습니다.", MessageType.Info);
                EditorGUILayout.LabelField("1. '대사'의 번역 키 / 고급 이벤트에서 줄별 키를 입력합니다.\n2. LocalizationTable.csv의 Key에 같은 키를 넣고 KR / EN / JP / CN 본문을 작성합니다.\n3. 언어를 바꿔 긴 문장·태그·빈 번역을 확인합니다.", ContentMakerGUI.Body);
                EditorGUILayout.Space(6);
                int missing = 0;
                if (dialogue.Nodes != null)
                    foreach (DialogueNode node in dialogue.Nodes)
                        if (node != null && string.IsNullOrWhiteSpace(node.LocalizationKey)) missing++;
                EditorGUILayout.LabelField($"번역 키가 없는 대사: {missing}줄", ContentMakerGUI.Body);
                using (new EditorGUI.DisabledScope(missing == 0))
                    if (ContentMakerGUI.SecondaryButton("빈 본문 키만 생성"))
                        Change(dialogue, "빈 본문 번역 키 생성", () =>
                        {
                            foreach (DialogueNode node in dialogue.Nodes)
                                if (node != null && string.IsNullOrWhiteSpace(node.LocalizationKey))
                                    node.LocalizationKey = "dlg." + Guid.NewGuid().ToString("N");
                            context.Report("빈 키만 생성했습니다. 기존 키는 유지됩니다. 번역표의 행은 별도로 작성하고 대화를 저장하세요.");
                        });
                EditorGUILayout.LabelField("생성한 키는 줄을 이동하거나 파일명을 바꿔도 유지됩니다. 대사를 복제하면 기존 키도 공유되므로, 다른 문장으로 바꿀 때는 키도 분리하세요.", ContentMakerGUI.Muted);
                if (ContentMakerGUI.SecondaryButton("번역표 위치 찾기"))
                {
                    TextAsset table = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/LocalizationTable.csv");
                    if (table != null) EditorGUIUtility.PingObject(table);
                    else context.Report("Assets/Resources/LocalizationTable.csv를 찾지 못했습니다.", MessageType.Warning);
                }
                EditorGUILayout.HelpBox("번역표 로더는 현재 물리적인 줄 단위로 읽습니다. 따옴표 안의 실제 줄바꿈을 포함한 번역 셀은 아직 지원하지 않습니다. 대본 CSV의 여러 줄 지원과 혼동하지 마세요. XLSX 직접 가져오기·번역표 자동 동기화도 지원하지 않습니다.", MessageType.Warning);
            }
        }

        private static void ExportCsv(ContentMakerContext context, DialogueData dialogue)
        {
            string csv = ContentMakerDialogueCsv.Export(dialogue);
            string path = EditorUtility.SaveFilePanel("대사 CSV 내보내기", string.Empty, dialogue.name, "csv");
            if (string.IsNullOrEmpty(path)) return;
            WriteCsvFile(path, csv);
            context.Report("UTF-8 CSV를 내보냈습니다. 엑셀에서 편집 후 검사/미리보기를 거쳐 가져오세요.");
        }

        private static void ExportSample(ContentMakerContext context)
        {
            string path = EditorUtility.SaveFilePanel("대사 샘플 CSV 저장", string.Empty, "Dialogue_Sample", "csv");
            if (string.IsNullOrEmpty(path)) return;
            DialogueData sample = ScriptableObject.CreateInstance<DialogueData>();
            try
            {
                sample.Nodes.Add(new DialogueNode
                {
                    DefaultText = "이곳을 둘러보시겠어요?\n선택지를 골라 주세요.",
                    IsChoiceNode = true,
                    Choices = new List<ChoiceData> { new ChoiceData { ChoiceText = "네, 둘러볼게요." }, new ChoiceData { ChoiceText = "나중에 올게요." } }
                });
                WriteCsvFile(path, ContentMakerDialogueCsv.Export(sample));
                context.Report("샘플 CSV를 저장했습니다. 빈 화자/다음대화 참조는 내레이션/대화 종료를 뜻합니다.");
            }
            finally { Object.DestroyImmediate(sample); }
        }

        private void ReadCsv(ContentMakerContext context, DialogueData dialogue)
        {
            ClearPendingImport();
            string path = EditorUtility.OpenFilePanel("대사 CSV 검사", string.Empty, "csv");
            if (string.IsNullOrEmpty(path)) return;
            if (new FileInfo(path).Length > 16L * 1024 * 1024)
                throw new InvalidOperationException("CSV가 16MB를 초과합니다. 대화를 나누어 가져오세요.");
            string csv = File.ReadAllText(path, new UTF8Encoding(false, true));
            ContentMakerDialogueImport candidate = ContentMakerDialogueCsv.Parse(csv);
            _pendingBaseline = EditorJsonUtility.ToJson(dialogue);
            _pendingCsv = csv;
            _pendingImport = candidate;
            _pendingTarget = dialogue;
            _pendingSource = path;
            _pendingWarnings.AddRange(ContentMakerDialogueCsv.GetWarnings(dialogue, candidate.Nodes));
            context.Report("CSV 검사가 완료되었습니다. 아직 대화는 변경하지 않았습니다.");
        }

        private void ApplyCsv(ContentMakerContext context, DialogueData dialogue)
        {
            if (_pendingImport == null || _pendingTarget != dialogue)
                throw new InvalidOperationException("적용할 CSV 미리보기가 없습니다.");
            if (_pendingBaseline != EditorJsonUtility.ToJson(dialogue))
                throw new InvalidOperationException("미리보기 이후 대상 대화가 수정되었습니다. 변경 내용을 저장/확인하고 CSV를 다시 검사하세요.");
            // Resolve all references again: assets may have been moved/deleted since preview.
            ContentMakerDialogueImport verified = ContentMakerDialogueCsv.Parse(_pendingCsv);
            string snapshot = EditorJsonUtility.ToJson(dialogue);
            try
            {
                Change(dialogue, "CSV 대화 전체 교체", () =>
                {
                    dialogue.Style = verified.Style;
                    dialogue.Nodes = verified.Nodes;
                });
                ContentMakerAssetUtility.Save(dialogue);
            }
            catch
            {
                EditorJsonUtility.FromJsonOverwrite(snapshot, dialogue);
                EditorUtility.SetDirty(dialogue);
                throw;
            }
            _nodeIndex = 0;
            ClearPendingImport();
            context.RefreshAssets();
            context.Report("CSV를 선택한 대화에 적용하고 저장했습니다. 실행 취소(Ctrl+Z) 후에는 다시 저장해 주세요.");
        }

        private static void WriteCsvFile(string path, string csv)
        {
            string fullPath = Path.GetFullPath(path);
            string assetsPath = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string packagesPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(packagesPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("기존 Unity 자산을 덮어쓰지 않도록 CSV는 Assets/Packages 밖의 작업 폴더에 저장하세요.");
            if (!string.Equals(Path.GetExtension(fullPath), ".csv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("내보내기 파일 확장자는 .csv여야 합니다.");
            // Write to a sibling file first so a failed write cannot truncate an existing export.
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, csv, new UTF8Encoding(true));
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null);
                else File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private void MoveNode(DialogueData dialogue, int offset)
        {
            Change(dialogue, "대사 순서 변경", () =>
            {
                int next = _nodeIndex + offset;
                DialogueNode node = dialogue.Nodes[_nodeIndex];
                dialogue.Nodes[_nodeIndex] = dialogue.Nodes[next];
                dialogue.Nodes[next] = node;
                _nodeIndex = next;
            });
        }

        private void Change(DialogueData dialogue, string label, Action change)
        {
            Undo.RecordObject(dialogue, label);
            change();
            EditorUtility.SetDirty(dialogue);
            _labelsDirty = true;
            _warnings.Clear();
        }

        private void RebuildLabels(DialogueData dialogue)
        {
            _nodeLabels = new string[dialogue.Nodes.Count];
            for (int index = 0; index < dialogue.Nodes.Count; index++)
            {
                DialogueNode node = dialogue.Nodes[index];
                string speaker = node?.Speaker != null ? node.Speaker.DisplayName : "내레이션";
                _nodeLabels[index] = $"{index + 1}. [{speaker}] {ShortText(node?.DefaultText, 50)}";
            }
            _labelsDirty = false;
        }

        private static DialogueNode CloneNode(DialogueNode source)
        {
            if (source == null) return new DialogueNode();
            var copy = new DialogueNode
            {
                Speaker = source.Speaker, Emotion = source.Emotion, LocalizationKey = source.LocalizationKey,
                DefaultText = source.DefaultText, EventTriggerID = source.EventTriggerID, IsChoiceNode = source.IsChoiceNode,
                Choices = new List<ChoiceData>()
            };
            if (source.Choices != null)
                foreach (ChoiceData choice in source.Choices)
                    copy.Choices.Add(choice == null ? null : new ChoiceData
                    {
                        ChoiceText = choice.ChoiceText, NextDialogue = choice.NextDialogue,
                        SetFlagOnSelect = choice.SetFlagOnSelect, StartBattleEncounter = choice.StartBattleEncounter
                    });
            return copy;
        }

        private static void DrawPortrait(SpeakerData speaker, EmotionType emotion)
        {
            if (speaker?.Portraits == null) return;
            Sprite portrait = speaker.GetPortrait(emotion);
            if (portrait == null) return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("표정 미리보기", ContentMakerGUI.Muted);
            Rect slot = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80));
            GUI.Box(slot, GUIContent.none, EditorStyles.helpBox);
            slot = new Rect(slot.x + 4, slot.y + 4, slot.width - 8, slot.height - 8);
            Texture texture = AssetPreview.GetAssetPreview(portrait);
            if (texture != null) GUI.DrawTexture(slot, texture, ScaleMode.ScaleToFit);
            else
            {
                // A non-readable sprite does not need a CPU pixel copy for editor preview.
                Rect uv = portrait.rect;
                uv.x /= portrait.texture.width;
                uv.width /= portrait.texture.width;
                uv.y /= portrait.texture.height;
                uv.height /= portrait.texture.height;
                float aspect = portrait.rect.width / Mathf.Max(1, portrait.rect.height);
                if (aspect > 1) { float height = slot.width / aspect; slot.y += (slot.height - height) * .5f; slot.height = height; }
                else { float width = slot.height * aspect; slot.x += (slot.width - width) * .5f; slot.width = width; }
                GUI.DrawTextureWithTexCoords(slot, portrait.texture, uv);
            }
            EditorGUILayout.Space(6);
        }

        private static string ShortText(string text, int length)
        {
            string oneLine = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            return oneLine.Length <= length ? oneLine : oneLine.Substring(0, length) + "…";
        }

        private void SetSpeaker(SpeakerData speaker)
        {
            _speakerTree?.Dispose();
            _speakerTree = null;
            _speaker = speaker;
        }

        private void ClearPendingImport()
        {
            _pendingImport = null;
            _pendingTarget = null;
            _pendingSource = null;
            _pendingBaseline = null;
            _pendingCsv = null;
            _pendingWarnings.Clear();
        }

        private static void Run(ContentMakerContext context, Action action)
        {
            try { action(); }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { context.Report(error.Message, MessageType.Error); }
        }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _rulesPanel.Dispose();
            SetSpeaker(null);
            ClearPendingImport();
        }
    }
}
