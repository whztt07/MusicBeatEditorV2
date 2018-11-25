﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System;
using JSON;
using System.Runtime.InteropServices;
using System.IO;


public class NoteProto
{
    public int type;
    public string name;
    public bool IsHold;
    public string key;
    [System.Obsolete]
    public int rightClip;

    public int exType;
    public Texture2D texture;
    public Texture2D bossTexture;
    public AudioClip audioClip;
}

public enum BeatItemType
{
    Type_NormalNote = 0,
    Type_ReverseNote = 1,
    Type_ComboNote = 2,
    Type_HoldNote,
    Type_BossComboNote,
}

public class MusicBeatDataEditor : EditorWindow
{
    
    #region Init
    [MenuItem("Tools/编辑器：谱面")]
    public static void ShowEditor()
    {        
        MusicBeatDataEditor editor = EditorWindow.GetWindow<MusicBeatDataEditor>();
        editor.Init();
        Application.runInBackground = true;
    }

    [MenuItem("Tools/强制销毁谱面编辑器")]
    public static void DestoryEditor()
    {
        EditorWindow.GetWindow<MusicBeatDataEditor>().Close();
    }

    #endregion

    private MusicBeatResScriptableObject _editorResSo;



    private EditorOp _editorOp;
    private MusicBeatData Data;
    private AudioSource _bgmSource;
    private AudioSource _fxSource;
    private List<string> noteIdentityTitles = new List<string>() {"普通", "Boss施放", "回血", "奖励" };
    private int _lineNum = 5;

    private Vector2 heightRange = new Vector2(-100, 100);
    private int _chasmHeight = -999;
    private Color _maxHeightColor = new Color(0.4f, 0.4f, 0.4f , 1);
    private Color _minHeightColor = new Color(0.9f, 0.9f, 0.9f, 1);
    private int _copyLen = 0;
    private int _obstacleIdInput = -1;
    private float _userData = 0;
    private int _targetCopyNoteIDElement = 0;
    private int _sceneID = 0;
    private int _bpmCount = 1;

    public void Init()
    {
        titleContent = new GUIContent("谱面编辑器");

        GameObject go = new GameObject();
        go.name = "AudioSource";
        _bgmSource = go.AddComponent<AudioSource>();
        _fxSource = go.AddComponent<AudioSource>();

        _editorResSo = AssetDatabase.LoadAssetAtPath<MusicBeatResScriptableObject>("Assets/Editor/MusicBeatEditor/MusicBeatRes.asset");

        Data = new MusicBeatData();
        Data.mbAudioSource._source = _bgmSource;
        Data._comboPerBeatInHold = _editorResSo._comboPerBeatInHold;

        try
        {
            Data.LoadMusicNames();
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("", "打开编辑器前,要先关闭MusicData这个Excel!!!" + "\n" + e, "OK");
            Close();
            return;
        }

        try
        {
            _noteProtos.Clear();
            BeatItemProtypes beatItemProtypes = BeatItemExporter.ReadExcel();
            if (beatItemProtypes != null)
            {
                NoteProto proto = new NoteProto();
                proto.type = -1;
                proto.name = "删除";
                _noteProtos.Add(proto);

                for (int i = 0; i < beatItemProtypes.beatItems.Count; i++)
                {
                    proto = new NoteProto();
                    proto.type = beatItemProtypes.beatItems[i]._type;
                    proto.name = beatItemProtypes.beatItems[i]._obstacleName;
                    proto.IsHold = beatItemProtypes.beatItems[i]._isHold == 1;
                    proto.key = beatItemProtypes.beatItems[i]._keyName;
                    proto.rightClip = beatItemProtypes.beatItems[i]._rightClickClip;
                    _noteProtos.Add(proto);

                    if (proto.IsHold)
                    {
                        //更改类型
                        proto.exType = (int)(EnumBeatItemTypeEx.eHoldBegin);

                        //第二个
                        proto = new NoteProto();
                        proto.type = beatItemProtypes.beatItems[i]._type;
                        proto.name = beatItemProtypes.beatItems[i]._obstacleName;
                        proto.IsHold = beatItemProtypes.beatItems[i]._isHold == 1;
                        proto.key = beatItemProtypes.beatItems[i]._keyName;
                        proto.rightClip = beatItemProtypes.beatItems[i]._rightClickClip;
                        proto.exType = (int)(EnumBeatItemTypeEx.eHoldEnd);
                        _noteProtos.Add(proto);
                    }


                }

                for (int i = 0; i < _noteProtos.Count; ++i)
                {
                    //_beatProtos[i].texture = MusicBeatDataHelper.GetBeatTexture(_beatProtos[i].type);
                    if (_noteProtos[i].type == -1)
                        _noteProtos[i].texture = _editorResSo._clearTexture;
                    else
                    {
                        _noteProtos[i].texture = _editorResSo._noteTextures[_noteProtos[i].type];
                        if (_noteProtos[i].exType == (int)EnumBeatItemTypeEx.eHoldEnd)
                        {
                            _noteProtos[i].texture = _editorResSo._noteEndTextures[_noteProtos[i].type];
                        }

                        if(_noteProtos[i].type == (int)BeatItemType.Type_NormalNote)
                        {
                            _noteProtos[i].bossTexture = _editorResSo._bossNoteNormal;
                        }
                        else if(_noteProtos[i].type == (int)BeatItemType.Type_ReverseNote)
                        {
                            _noteProtos[i].bossTexture = _editorResSo._bossNoteReverse;
                        }

                        //有部分音效是命中反馈的,  上跳的音效绑定在动作上, 这样空按的时候也有声音
                        //无法再使用表里的数据, 表里关于上跳的音效配置到动作里了, 此处都由策划们自己填
                        _noteProtos[i].audioClip = _editorResSo._NoteAudioClips[_noteProtos[i].type];
                        if (_noteProtos[i].exType == (int)EnumBeatItemTypeEx.eHoldEnd)
                        {
                            _noteProtos[i].audioClip = _editorResSo._NoteEndAudioClips[_noteProtos[i].type];
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("", "打开编辑器前,要先关闭BeatItem_障碍物信息.xlsx" + "\n" + e, "OK");
            Close();
            return;
            
        }
        

        _editorOp = new EditorOp();
    }


    
    void ShowHud()
    {
        if (string.IsNullOrEmpty(curMusicFilePath))
            return;
        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("运行时");

            EditorGUILayout.LabelField("歌曲名称\t" + Data.musicName);

            EditorGUILayout.LabelField("谱面时长\t" + Data.chartLength);
            EditorGUILayout.LabelField("音乐偏移\t" + Data.musicOffset);

            float currbpm = Data.GetBpmNew(_currentBeat);
            EditorGUILayout.LabelField("当前 BPM\t" + currbpm);

            float selectbpm = Data.GetBpmNew(_selectBeat);
            EditorGUILayout.LabelField("选中 BPM\t" + selectbpm);



            EditorGUILayout.LabelField("拍数\t" + Data.floatBeatCount);
            EditorGUILayout.LabelField("拍数\t" + Data.beatCount);
            EditorGUILayout.LabelField("拍时\t" + Data.oneBeatLength);

            GUILayout.Label("音乐时间\t" + Data.mbAudioSource.time);
            GUILayout.Label("谱面时间\t" + Data.chartTime);

            GUILayout.Label(string.Format("频道\t{0}", _selectLine));
            GUILayout.Label(string.Format("当前\t{0}/{1}", _currentBeat, _currentBeatNote));
            GUILayout.Label(string.Format("选中\t{0}/{1}", _selectBeat, _selectBeatNote));


            _bgmSource.volume = EditorGUILayout.Slider("音乐",_bgmSource.volume, 0, 1);
            _fxSource.volume = EditorGUILayout.Slider("音效", _fxSource.volume, 0, 1);

            float currMaxTerrain = Data.GetMaxTerrain(_currentBeat);
            GUILayout.Label("当前最长沟壑数\t" + currMaxTerrain);

            float selectMaxTerrain = Data.GetMaxTerrain(_selectBeat);
            GUILayout.Label("选中最长沟壑数\t" + selectMaxTerrain);
        }
        EditorGUILayout.EndVertical();
        
    }

    void PlayOrPause()
    {
        _progress = Data.Proportion;
        Data.mbAudioSource.PlayOrPause();

        if(!Data.mbAudioSource._source.isPlaying)
            _fxSource.Stop();
    }


    #region GUI
    void OnGUI()
    {
        UserInput();
        //return;

        ShowMenuBar();


        ShowGSceneBeat();
        Repaint();
    }

    void ShowMenuBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            string dir = Application.dataPath;
            dir += "/Resources/MusicChart";
            dir = EditorUtility.SaveFilePanel("新建谱面", dir, "", "json");
            if (!string.IsNullOrEmpty(dir))
            {
                curMusicFilePath = dir;
                Data.CreateChart();
                Save(curMusicFilePath);
                LoadMusicData(curMusicFilePath);
            }
        }
        if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            string dir = Application.dataPath;
            dir += "/Resources/MusicChart";
            Debug.Log("加载数据: " + dir);
            string tmp = EditorUtility.OpenFilePanel("选择谱面", dir, "txt,json");
            if (!string.IsNullOrEmpty(tmp))
            {
                curMusicFilePath = tmp;
                LoadMusicData(tmp);
            }

        }
        bool b = !string.IsNullOrEmpty(curMusicFilePath);
        if (b && GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            Save(curMusicFilePath);
        }
        if (b && GUILayout.Button("另存为", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            string dir = Application.dataPath;
            dir += "/Resources/MusicChart";
            var path = EditorUtility.SaveFilePanel("数据另存为", dir, "", "json");
            if (path.Length != 0)
            {
                Save(path);
            }
        }

        if (b && GUILayout.Button("检查", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            string ret = "";
            bool succ = Data.Statistics(ref ret);
            string title = succ ? "检查通过" : "检查失败";
            EditorUtility.DisplayDialog(title, ret, title);
        }

        if (b && GUILayout.Button("复制氛围", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            string dir = Application.dataPath;
            dir += "/Resources/MusicChart";
            var path = EditorUtility.OpenFilePanel("复制氛围", dir, "txt");
            if (path.Length != 0)
            {
                string content = File.ReadAllText(path);
                Data.CopyTriggersData(content);
            }
        }



        if (!b && GUILayout.Button("导出统计数据", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var path = EditorUtility.SaveFilePanel("导出统计数据", dir, "1.bytes", "bytes");
            if (path.Length != 0)
            {
                EditorUtility.DisplayProgressBar("导出统计信息", "开始", 0f);
                string txt = Report(true);
                EditorUtility.DisplayProgressBar("导出统计数据", "写文件", 0.99f);
                File.WriteAllText(path, txt);
                EditorUtility.ClearProgressBar();

#if UNITY_EDITOR_WIN
                string folder = Directory.GetParent(path).FullName;
                string winPath = folder.Replace("/", "\\"); // windows explorer doesn't like forward slashes
                System.Diagnostics.Process.Start("explorer.exe", ("/root,") + winPath);
#endif
            }
        }
        if (GUILayout.Button("", EditorStyles.toolbarButton))
        {

        }

        EditorGUILayout.EndHorizontal();
    }

    void OnDestroy()
    {
        Debug.Log("关闭窗口，销毁音源");

        _bgmSource.Stop();
        _fxSource.Stop();
        GameObject.DestroyImmediate(_bgmSource.gameObject);
        _bgmSource = null;
        _fxSource = null;
    }

    #endregion

    #region 变量
    public int _selectLine = 1;
    public int _currentBeat = -1;
    public int _currentBeatNote = -1;
    public int _selectBeat = -1;
    public int _selectBeatNote = -1;

    float _progress;

    string curMusicFilePath = "";

    string[] stringBeatSplitAry = { "2","3", "4", "6", "8", "12", "16", "24", "32"};
    int[] intBeatSplitAry = { 2,3,4,6,8,12,16,24,32 };


    Vector3 SVPos;
    Vector2 GToolSVPos;
    /// <summary>
    /// 数据中心
    /// </summary>
    

    #endregion
    public void UpdatePlayingBeat()
    {
        float time = Data.chartTime;
        //float fbeat = time/Data.getOneBeatLength(_currentBeat);
        float fbeat = Data.GetBeatByAudioTime(Data.chartTime);
        int value = (int) fbeat;
        if (_currentBeat != value)
        {            
            _currentBeat = value;
            _currentBeatNote = 0;
            OnEnterPlayingNewBeat();
            OnEnterPlayingNewNote();
        }
        else
        {
            MusicBeatJson m = Data.GetMusicBeatJson(_currentBeat);
            if (m != null)
            {
                

                //float f1 = _currentBeat * Data.getOneBeatLength(_currentBeat);
                float f1 = Data.getAudioTimeByBeat(_currentBeat);
                float f2 = time - f1;
                float f3 = f2/m.noteTime;
                value = Mathf.FloorToInt(f3);

                if (_currentBeatNote != value)
                {
                    _currentBeatNote = value;
                    OnEnterPlayingNewNote();
                }
            }
        }
    }
    void OnEnterPlayingNewBeat()
    {
        
    }

    void OnEnterPlayingNewNote()
    {
        if (!Data.mbAudioSource._source.isPlaying)
            return;

        MusicBeatNoteJson upNote = Data.GetNote(_currentBeat, _currentBeatNote, EnumLineType.UpNote);
        if(upNote != null)
        {
            PlayNote(upNote);
        }

        MusicBeatNoteJson downNote = Data.GetNote(_currentBeat, _currentBeatNote, EnumLineType.DownNote);
        if(downNote != null)
        {
            PlayNote(downNote);
        }
    }
    
    void PlayNote(MusicBeatNoteJson note)
    {
        if (note != null)
        {
            int beatType = note.type;
            EnumBeatItemType eType = (EnumBeatItemType)(note.type);
            EnumBeatItemTypeEx exType = (EnumBeatItemTypeEx)(note.exType);



            if (exType == EnumBeatItemTypeEx.eNone)
            {
                //int musicId = GetNoteProto(note.type, note.exType).rightClip;
                //string soundEffectName = Data.GetMusicClipName(musicId);
                //if (!string.IsNullOrEmpty(soundEffectName))
                //{
                //    MusicBeatDataHelper.PlaySouce(_fxSource, soundEffectName);
                //}

                AudioClip clip = GetNoteProto(note.type, note.exType).audioClip;
                if (clip != null)
                {
                    MusicBeatDataHelper.PlaySouce(_fxSource, clip);
                }
            }
            else if (exType == EnumBeatItemTypeEx.eHoldBegin)
            {
                //int musicId = GetNoteProto(note.type, note.exType).rightClip;
                //string soundEffectName = Data.GetMusicClipName(musicId);
                //if (!string.IsNullOrEmpty(soundEffectName))
                //{
                //    MusicBeatDataHelper.PlaySouce(_fxSource, soundEffectName, true);
                //}

                AudioClip clip = GetNoteProto(note.type, note.exType).audioClip;
                if (clip != null)
                {
                    MusicBeatDataHelper.PlaySouce(_fxSource, clip, true);
                }
            }
            else if (exType == EnumBeatItemTypeEx.eHoldEnd) // hold松开的时候
            {
                _fxSource.Stop();

                //int musicId = GetNoteProto(note.type, note.exType).rightClip;
                //string soundEffectName = Data.GetMusicClipName(musicId);
                //if (!string.IsNullOrEmpty(soundEffectName))
                //{
                //    MusicBeatDataHelper.PlaySouce(_fxSource, soundEffectName);
                //}

                AudioClip clip = GetNoteProto(note.type, note.exType).audioClip;
                if (clip != null)
                {
                    MusicBeatDataHelper.PlaySouce(_fxSource, clip);
                }
            }
        }


    }
    void ShowGSceneBeat()
    {
        if (string.IsNullOrEmpty(curMusicFilePath))
            return;
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.BeginVertical("box");
            {
                ShowGSceneBeatEx();
            }
            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginVertical("box", GUILayout.MaxWidth(300));
            {
                ShowTool();
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();
    }

    void ShowGSceneBeatEx()
    {
        float AllCount = Data.floatBeatCount;
        int ShowCount = 0;
        float allpiex = AllCount * MusicBeatDataHelper.BeatWidth;
        int StartCount = 0;
        int LeftEdgeCount = 0;
        float origin = 480;
        

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(MusicBeatDataHelper.GUITextW);
        {
            EditorGUILayout.LabelField("", MusicBeatDataHelper.GUITextW, GUILayout.Height(MusicBeatDataHelper.BtnH * 2));
            for (int i = 0; i < Data.Lines.Count; i++)
            {
                EditorGUILayout.LabelField(Data.Lines[i].LineName, MusicBeatDataHelper.GUITextW, MusicBeatDataHelper.GUIBtnH);
            }
        }
        EditorGUILayout.EndVertical();

        Rect winRect = this.position;
        SVPos = EditorGUILayout.BeginScrollView(SVPos, false, false, "button", "button", "AnimationKeyframeBackground", GUILayout.Height(500), GUILayout.MinWidth(winRect.width - 400));

        float timebak1 = Data.mbAudioSource.time;
        float timebak2 = EditorGUILayout.Slider(Data.mbAudioSource.time, 0, Data.mbAudioSource.length);
        if (timebak2 != timebak1)
        {
            Data.mbAudioSource.time = timebak2;
        }

        ShowCount = (int)Mathf.CeilToInt(winRect.width / MusicBeatDataHelper.BeatWidth);


        // StartCount = (int)(Mathf.Clamp(AllCount * _progress, 0, AllCount));
        StartCount = Data.GetBeatByAudioTime(Data.chartTime);

         LeftEdgeCount = StartCount - (int)(origin/MusicBeatDataHelper.BeatWidth + 1);
        int DrawB = 2;
        for (int lineIdx = 0; lineIdx < Data.Lines.Count; lineIdx++)
        {
            for (int i = LeftEdgeCount; i < LeftEdgeCount + ShowCount; i++)
            {
                if(i<0 || i>=Data.beatCount)
                    continue;

                //float beatX = i*MusicBeatDataHelper.BeatWidth + origin - _progress*allpiex;
                //float progress = 
                float beatX = i * MusicBeatDataHelper.BeatWidth + origin - StartCount * MusicBeatDataHelper.BeatWidth;
                Rect rect = new Rect(beatX, DrawB * (MusicBeatDataHelper.BtnH + 1), MusicBeatDataHelper.BeatWidth, MusicBeatDataHelper.BtnH);
                EBeatLine currLine = Data.Lines[lineIdx];

                //时间显示	
                if (currLine.EventType ==  EnumLineType.Tick)
                {
                    rect.width = MusicBeatDataHelper.FixedBtnW;
                    GUI.Label(rect, i.ToString(), "AssetLabel");
                    Handles.DrawLine(new Vector3(rect.x, rect.y, 0), new Vector3(rect.x, rect.y + rect.height, 0));

                    MusicBeatJson mbeat = Data.GetMusicBeatJson(i);
                    int split = mbeat.split;

                    if (split >= 6)
                    {
                        int cnt = split / 4;
                        if (split == 6 || split == 12 || split == 24)
                        {
                            cnt = split/3;
                        }
                        float bw = MusicBeatDataHelper.BeatWidth / cnt;
                        for (int s = 1; s < cnt; ++s)
                        {
                            rect.x = beatX + s * bw;
                            Handles.DrawLine(new Vector3(rect.x, rect.y + rect.height / 2, 0), new Vector3(rect.x, rect.y + rect.height, 0));
                        }

                    }
                }
                //节拍
                else if (
                    (currLine.EventType == EnumLineType.UpNote)
                    ||(currLine.EventType == EnumLineType.DownNote)
                    )
                {
                    MusicBeatJson mbeat = Data.GetMusicBeatJson(i);
                    int split = mbeat.split;
                    
                    if (split > 0)
                    {
                        float bw = MusicBeatDataHelper.BeatWidth/split;

                        for (int j = 0; j < split; ++j)
                        {
                            rect.x = beatX + j*bw;
                            rect.width = bw;

                            GUI.backgroundColor = Color.white;
                            if (i == _currentBeat && j==_currentBeatNote)
                            {
                                GUI.backgroundColor = Color.green;
                            }

                            if (_selectLine == lineIdx && i == _selectBeat && j == _selectBeatNote)
                            {
                                GUI.backgroundColor = Color.yellow;
                            }

              
                            
                            if (GUI.Button(rect, "", _editorResSo._skin.button))
                            {
                                _selectLine = lineIdx;
                                _selectBeat = i;
                                _selectBeatNote = j;

                                //右键删除
                                if (Event.current != null && Event.current.button == 1)
                                {
                                    Data.RemoveNote(_selectBeat, _selectBeatNote, currLine.EventType);
                                }
                            }

                            MusicBeatNoteJson m = Data.GetNote(i, j, currLine.EventType);
                            if (m != null)
                            {
                                NoteProto b = GetNoteProto(m.type, m.exType);
                                Texture2D tex = null;
                                if (b != null)
                                {
                                    if(m.noteIdentity == (int)MusicBeatNoteIdentityType_Noraml.Type_FromBoss_Skill_1 || m.noteIdentity == (int)MusicBeatNoteIdentityType_Noraml.Type_FromBoss_Skill_2)
                                    {
                                        tex = b.bossTexture;
                                    }
                                    else
                                    {
                                        tex = b.texture;
                                    }
                                    
                                    //tex = b.texture;
                                    GUI.DrawTexture(rect, tex);
                                }

                                if (m.yeah > 0)
                                {
                                    GUI.DrawTexture(rect, _editorResSo._yeahTexture);
                                }

                                GUI.Label(rect, "ID:" + m.noteID.ToString(), EditorStyles.wordWrappedLabel);
                            }
                        }
                    }
                }
                //障碍
                else if(currLine.EventType == EnumLineType.Obstacle)
                {
                    MusicBeatJson mbeat = Data.GetMusicBeatJson(i);
                    int split = mbeat.split;

                    if (split > 0)
                    {
                        float bw = MusicBeatDataHelper.BeatWidth / split;

                        for (int j = 0; j < split; ++j)
                        {
                            rect.x = beatX + j * bw;
                            rect.width = bw;
                            MusicBeatObstacleJosn m = Data.GetObstacle(i, j);

                            GUI.backgroundColor = Color.white;

                            if (m != null)
                            {
                                if(m.identity == (int)MusicBeatObstacleIdentityType.Type_Obstacle)
                                {
                                    GUI.backgroundColor = Color.red;                           
                                }
                                else if(m.identity == (int)MusicBeatObstacleIdentityType.Type_Fly_X)
                                {
                                    GUI.backgroundColor = new Color(0.2f, 1, 0.2f);
                                }
                                else if(m.identity == (int)MusicBeatObstacleIdentityType.Type_Fly_Y)
                                {
                                    GUI.backgroundColor = new Color(0.2f, 0.2f, 1);
                                }
                            }

                            if (_selectLine == lineIdx && i == _selectBeat && j == _selectBeatNote)
                            {
                                GUI.backgroundColor = Color.yellow;
                            }

                            if (GUI.Button(rect, "", _editorResSo._skin.button))
                            {
                                _selectLine = lineIdx;
                                _selectBeat = i;
                                _selectBeatNote = j;

                                //右键删除
                                if (Event.current != null && Event.current.button == 1)
                                {
                                    Data.RemoveObstacle(_selectBeat, _selectBeatNote);
                                }
                            }


                            if (m != null)
                            {
                                GUI.Label(rect, "ID:" + m.obstacleID.ToString(), EditorStyles.wordWrappedLabel);
                            }
                        }
                    }
                }
                //地面
                else if (currLine.EventType == EnumLineType.Floor)
                {
                    MusicBeatJson mbeat = Data.GetMusicBeatJson(i);
                    int split = mbeat.split;

                    if (split > 0)
                    {
                        float bw = MusicBeatDataHelper.BeatWidth / split;

                        for (int j = 0; j < split; ++j)
                        {
                            rect.x = beatX + j * bw;
                            rect.width = bw;
                            MusicBeatFloorJson m = Data.GetFloor(i, j);

                            GUI.backgroundColor = Color.white;

                            if (m != null)
                            {
                                //GUI.backgroundColor = Color.cyan;
                                if(m.heightlevel == _chasmHeight)
                                {
                                    GUI.backgroundColor = Color.black;
                                }
                                else
                                {
                                    float total = (int)heightRange.y - (int)heightRange.x;
                                    int offset = (int)(total / 2) + 1;
                                    float lerp = ((float)(m.heightlevel + offset) / (float)total);
                                    Color backColor = Color.Lerp(_minHeightColor, _maxHeightColor, lerp);
                                    GUI.backgroundColor = backColor;
                                }

                            }
                            
                            if (_selectLine == lineIdx && i == _selectBeat && j == _selectBeatNote)
                            {
                                GUI.backgroundColor = Color.yellow;
                            }

                            if (GUI.Button(rect, "", _editorResSo._skin.button))
                            {
                                _selectLine = lineIdx;
                                _selectBeat = i;
                                _selectBeatNote = j;

                                ////右键删除
                                //if (Event.current != null && Event.current.button == 1)
                                //{
                                //    Data.RemoveFloor(_selectBeat, _selectBeatNote);
                                //}
                            }

                            
                            if (m != null)
                            {
                                GUI.Label(rect, "H:" + m.heightlevel.ToString(), EditorStyles.wordWrappedLabel);
                            }
                        }
                    }
                }
                //环境
                else if (currLine.EventType == EnumLineType.Env)
                {
                    MusicBeatJson mbeat = Data.GetMusicBeatJson(i);
                    int split = mbeat.split;

                    if (split > 0)
                    {
                        float bw = MusicBeatDataHelper.BeatWidth / split;

                        for (int j = 0; j < split; ++j)
                        {
                            rect.x = beatX + j * bw;
                            rect.width = bw;
                            MusicBeatEnvJson m = Data.GetEnv(i, j);

                            GUI.backgroundColor = Color.white;

                            if (m != null)
                            {          
                                GUI.backgroundColor = Color.cyan;
                            }

                            if (_selectLine == lineIdx && i == _selectBeat && j == _selectBeatNote)
                            {
                                GUI.backgroundColor = Color.yellow;
                            }

                            if (GUI.Button(rect, "", _editorResSo._skin.button))
                            {
                                _selectLine = lineIdx;
                                _selectBeat = i;
                                _selectBeatNote = j;

                                //右键删除
                                if (Event.current != null && Event.current.button == 1)
                                {
                                    Data.RemoveEnv(_selectBeat, _selectBeatNote);
                                }
                            }

                            if (m != null)
                            {
                                if(m.list[0] == (int)MusicBeatEnvIdentityType.Type_BossAppear)
                                {
                                    GUI.Label(rect, "Appear", EditorStyles.wordWrappedLabel);                               
                                }
                                else if(m.list[0] == (int)MusicBeatEnvIdentityType.Type_BossDisappera)
                                {
                                    GUI.Label(rect, "Disappear", EditorStyles.wordWrappedLabel);
                                }
                                else if(m.list[0] == (int)MusicBeatEnvIdentityType.Type_SpeedScale)
                                {
                                    GUI.Label(rect, "SpeedScale:" + m.userData, EditorStyles.wordWrappedLabel);
                                }
                                else if(m.list[0] == (int)MusicBeatEnvIdentityType.Type_UserGuide)
                                {
                                    GUI.Label(rect, "UserGuide:" + m.userData, EditorStyles.wordWrappedLabel);
                                }

                            //    GUI.Label(rect, m.userData.ToString(), EditorStyles.wordWrappedLabel);
                            }
                        }
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            DrawB++;
        }
        
        Handles.color = Color.red;
        Handles.DrawLine(new Vector3(origin, 0, 0), new Vector3(origin, 400, 0));

        GUILayout.FlexibleSpace();
        ShowEditAction();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();
    }

    void ShowEditAction()
    {
        EditorGUILayout.BeginHorizontal();
        {
            float fw = 60;
            float fh = 60;
            

            if (GUILayout.Button("<<", GUILayout.Width(fw), GUILayout.Height(fh)))
            {
                Data.MusicBack();
            }
            if (GUILayout.Button(">>", GUILayout.Width(fw), GUILayout.Height(fh)))
            {
                Data.MusicForward();
            }

            if (GUILayout.Button("-", GUILayout.Width(fw), GUILayout.Height(fh)))
            {
                MusicBeatDataHelper.GUIBtnWScale /= 2f;
                MusicBeatDataHelper.GUIBtnWScale = Mathf.Clamp(MusicBeatDataHelper.GUIBtnWScale, 0f, 1f);
            }

            if (GUILayout.Button("+", GUILayout.Width(fw), GUILayout.Height(fh)))
            {
                MusicBeatDataHelper.GUIBtnWScale *= 2f;
                MusicBeatDataHelper.GUIBtnWScale = Mathf.Clamp(MusicBeatDataHelper.GUIBtnWScale, 0f, 1f);
            }

            GUIContent con = EditorGUIUtility.IconContent("PlayButton");
            if (GUILayout.Button(con, GUILayout.Width(fw), GUILayout.Height(fh)))
            {
                PlayOrPause();
            }

            int historyCount = Data.HistoryCount();
            if (historyCount > 0 && GUILayout.Button("撤销" + historyCount, GUILayout.Width(fw), GUILayout.Height(fh)))
            {
                GUI.FocusControl("");
                Data.Undo();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        {
            _editorOp.splitFromBeat = EditorGUILayout.IntField(_editorOp.splitFromBeat);
            _editorOp.splitToBeat = EditorGUILayout.IntField(_editorOp.splitToBeat);
            _editorOp.splitIndex = EditorGUILayout.Popup(_editorOp.splitIndex, stringBeatSplitAry);
            if (GUILayout.Button("分", GUILayout.Width(MusicBeatDataHelper.FixedBtnW)))
            {
                _editorOp.splitNum = intBeatSplitAry[_editorOp.splitIndex];
                if (!Data.SplitBeat(_editorOp.splitFromBeat, _editorOp.splitToBeat, _editorOp.splitNum))
                {

                }
            }


            _editorOp.copyRangeStart = EditorGUILayout.IntField(_editorOp.copyRangeStart);
            _editorOp.copyRangeEnd = EditorGUILayout.IntField(_editorOp.copyRangeEnd);
            _editorOp.copyToBeat = EditorGUILayout.IntField(_editorOp.copyToBeat);
            if (GUILayout.Button("剪切", GUILayout.Width(MusicBeatDataHelper.FixedBtnW)))
            {
                if (Cut())
                {

                }
            }
            if (GUILayout.Button("复制", GUILayout.Width(MusicBeatDataHelper.FixedBtnW)))
            {
                if (Copy())
                {

                }
            }
            if (GUILayout.Button("粘贴", GUILayout.Width(MusicBeatDataHelper.FixedBtnW)))
            {
                if (Paste())
                {

                }
            }

            if (GUILayout.Button("删除", GUILayout.Width(MusicBeatDataHelper.FixedBtnW)))
            {
                string str = string.Format("你要删除节拍{0}-{1}?", _editorOp.copyRangeStart, _editorOp.copyRangeEnd);
                if (EditorUtility.DisplayDialog("即将删除", str, "删除", "取消"))
                {
                    if (Delete())
                    {

                    }
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    #region 歌曲序列化与反序列化

    private System.Collections.Generic.List<NoteProto> _noteProtos = new System.Collections.Generic.List<NoteProto>();

    void ShowBaseInfo()
    {
        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("Hold每拍Combo数:", Data._comboPerBeatInHold.ToString());
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("谱面基本");
            EditorGUILayout.LabelField("名字:", Path.GetFileNameWithoutExtension(curMusicFilePath));

            int index = Data._musicIdsArrayIndex;
            Data._musicIdsArrayIndex = EditorGUILayout.Popup("音乐Id", Data._musicIdsArrayIndex, Data._musicIdsArray);
            if (index != Data._musicIdsArrayIndex)
            {
                Data.ChangeMusic();
            }
            EditorGUILayout.FloatField("音乐时长", Data.musicLength);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("修改");
            bool bBeatCountChange = false;
            EditorGUILayout.BeginHorizontal();
            {
                _editorOp.chartLength = EditorGUILayout.FloatField("谱面时长", _editorOp.chartLength);
                _editorOp.chartLength = Mathf.Clamp(_editorOp.chartLength, 0, Data.musicLength);
                if (GUILayout.Button("OK"))
                {
                    if(EditorUtility.DisplayDialog("提示", "确定吗？", "确定", "没想好"))
                    {
                        Data.chartLength = _editorOp.chartLength;
                        bBeatCountChange = true;
                        _editorOp.chartLength = 0;
                   
                    }

                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            {
                _editorOp.musicOffset = EditorGUILayout.FloatField("音乐偏移", _editorOp.musicOffset);
                _editorOp.musicOffset = Mathf.Clamp(_editorOp.musicOffset, 0, Data.musicLength);
                if (GUILayout.Button("OK"))
                {
                    Data.musicOffset = _editorOp.musicOffset;
                    bBeatCountChange = true;
                    _editorOp.musicOffset = 0;
                }
            }
            EditorGUILayout.EndVertical();


            
            EditorGUILayout.BeginHorizontal();
            {
                _editorOp.bpm = EditorGUILayout.FloatField("BPM", _editorOp.bpm);
                if (GUILayout.Button("OK"))
                {
                    Data.bpm = _editorOp.bpm;
                    bBeatCountChange = true;
                    _editorOp.bpm = 0;
                }
       
            }
            
            EditorGUILayout.EndVertical();

            if (bBeatCountChange)
            {
                _selectBeatNote = _selectBeat = -1;
                _selectLine = 1;
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box");
        {
            Data.sceneID = EditorGUILayout.IntField("场景ID\t", Data.sceneID);
        }
        EditorGUILayout.EndVertical();


        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("添加BPM", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("+"))
                {
                    Data.AddBpmInfo(60, 0);
                    //Data.AddEnv(_selectBeat, _selectBeatNote, (int)_editorOp.envIndenty);
                    //_editorOp.envIndenty = MusicBeatEnvIdentityType.Type_BossAppear;
                    GUI.FocusControl("");

                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("BPM信息", EditorStyles.boldLabel);

            List<MusicBpmInfo> bpmInfoTbl = Data.bpmTbl;
            for (int index = 0; index < bpmInfoTbl.Count; index++)
            {
                MusicBpmInfo currInfo = bpmInfoTbl[index];
                currInfo.bpm = EditorGUILayout.FloatField("bpm:", currInfo.bpm);
                currInfo.startBeat = EditorGUILayout.IntField("start:", currInfo.startBeat);
              
                if (GUILayout.Button("-"))
                {
                    Data.RemoveBpmInfo(index);
                }
            }

            if(bpmInfoTbl.Count >= 2)
            {
                if (GUILayout.Button("apply"))
                {
                    Data.ApplyBpmChange();
                }
            }
        }
        EditorGUILayout.EndVertical();
    }
    void ShowTool()
    {
        ShowBaseInfo();
        ShowHud();
        

        GToolSVPos = EditorGUILayout.BeginScrollView(GToolSVPos);
        EBeatLine currLine = Data.Lines[_selectLine];

        //EnumLineType eType = (EnumLineType) (_selectLine);
        switch (currLine.EventType)
        {
            case EnumLineType.UpNote:
            case EnumLineType.DownNote:
                ShowNotesList(currLine.EventType);
                break;
            case EnumLineType.Obstacle:
                ShowObstacleList();
                break;
            case EnumLineType.Floor:
                ShowFloorArea();
                break;
            case EnumLineType.Env:
                ShowEnvArea();
                break;
        }
        EditorGUILayout.EndScrollView();
    }
    
    void Save(string filepath)
    {
        Data.Save(filepath);
    }


    void LoadMusicData(string dataPath)
    {
        _editorOp.Clear();

        Data.LoadChart(dataPath);
    }

    #endregion

    #region Editor

    private int _beatItemPerLine = 6;
    private NoteProto GetNoteProto(int type, int extype)
    {
        for (int i = 0; i < _noteProtos.Count; i++)
        {
            if (_noteProtos[i].type == type && _noteProtos[i].exType == extype)
            {
                return _noteProtos[i];
            }
        }
        return null;
    }
    void ShowNotesList(EnumLineType eType)
    {
        GUIStyle btnStype = _editorResSo._skin.button;
        float fixW = MusicBeatDataHelper.FixedBtnW;
        float fixH = MusicBeatDataHelper.FixedBtnH;

        int colCnt = _beatItemPerLine;
        int rowCnt = _noteProtos.Count / colCnt + 1;

        int removeType = (int)EnumBeatItemType.eNone;
        EditorGUILayout.BeginVertical();
        for (int i = 0; i < rowCnt; ++i)
        {
            EditorGUILayout.BeginHorizontal();
            {
                for (int j = 0; j < colCnt; ++j)
                {
                    int idx = i * colCnt + j;
                    if (idx >= _noteProtos.Count)
                        break;

                    if (GUILayout.Button(_noteProtos[idx].texture, btnStype, GUILayout.Width(fixW), GUILayout.Height(fixH)))
                    {
                        if (_noteProtos[idx].type == removeType)
                        {
                            Data.RemoveNote(_selectBeat, _selectBeatNote, eType);
                        }
                        else
                        {
                            int yeah = Data.GetNoteYeah(_selectBeat, _selectBeatNote, eType);
                            MusicBeatNoteJson note = Data.SetNote(_selectBeat, _selectBeatNote, _noteProtos[idx].type, _noteProtos[idx].exType, yeah, eType);
                            if(note.type == (int)EnumBeatItemType.eBossNote)
                            {
                                note.noteIdentity = (int)_editorOp.bossNoteIndentityTypeTpl;
                            }
                        }
                    }

                    if (_noteProtos[idx].type != removeType)
                    {
                        if (_noteProtos[idx].exType == (int)EnumBeatItemTypeEx.eHoldEnd)
                        {
                            EditorGUI.LabelField(GUILayoutUtility.GetLastRect(), "结束");
                        }
                        else
                        {
                            EditorGUI.LabelField(GUILayoutUtility.GetLastRect(), _noteProtos[idx].name);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
        MusicBeatNoteJson currNote = Data.GetNote(_selectBeat, _selectBeatNote, eType);
        if(currNote != null)
        {
            currNote.noteID = EditorGUILayout.IntField("Note ID", currNote.noteID);
            int identity = 0;
            if(currNote.type == (int)EnumBeatItemType.eBossNote)
            {
                MusicBeatNoteIdentityType_Boss type = (MusicBeatNoteIdentityType_Boss)EditorGUILayout.EnumPopup("Note Identity", (MusicBeatNoteIdentityType_Boss)currNote.noteIdentity);
                identity = (int)type;
            }
            else if(currNote.type == (int)EnumBeatItemType.eLaunchNote)
            {
                MusicBeatNoteIdentityType_Launch type = (MusicBeatNoteIdentityType_Launch)EditorGUILayout.EnumPopup("Note Identity", (MusicBeatNoteIdentityType_Launch)currNote.noteIdentity);
                identity = (int)type;   
            }
            else{
                MusicBeatNoteIdentityType_Noraml type = (MusicBeatNoteIdentityType_Noraml)EditorGUILayout.EnumPopup("Note Identity", (MusicBeatNoteIdentityType_Noraml)currNote.noteIdentity);
                identity = (int)type;
            }

            currNote.noteIdentity = identity;

            _targetCopyNoteIDElement = EditorGUILayout.IntField("ID复制目标节拍\t", _targetCopyNoteIDElement);
            if (GUILayout.Button("copy"))
            {
                Data.CopyNoteId(currNote.noteID, _selectBeat, _selectBeatNote,  _targetCopyNoteIDElement, eType);
                Data.CopyIdentity(currNote.noteIdentity, _selectBeat, _selectBeatNote, _targetCopyNoteIDElement, eType);
            }
        }
        else
        {
            EditorGUILayout.IntField("Note ID", -1);
            EditorGUILayout.EnumPopup("Note Identity", MusicBeatNoteIdentityType_Noraml.Type_Normal);


        }


        EditorGUILayout.BeginVertical();


        EditorGUILayout.EndVertical();

    }

    void ShowObstacleList()
    {
        EditorGUILayout.BeginVertical("box");
        {

            MusicBeatObstacleJosn m = Data.GetObstacle(_selectBeat, _selectBeatNote);
            if (m != null)
            {
                m.obstacleID = EditorGUILayout.IntField("ID:", m.obstacleID);
                MusicBeatObstacleIdentityType type = (MusicBeatObstacleIdentityType)EditorGUILayout.EnumPopup("Identity", (MusicBeatObstacleIdentityType)m.identity);
                m.identity = (int)type;
            }
            else
            {
                int obstacleId = _obstacleIdInput;
                obstacleId = EditorGUILayout.IntField("ID:", obstacleId);
                if(obstacleId > 0)
                {
                    Data.AddObstacle(_selectBeat, _selectBeatNote, obstacleId);
                }
            }
        }
        EditorGUILayout.EndVertical();
    }

    void ShowFloorArea()
    {
        EditorGUILayout.BeginVertical("box");
        {

            MusicBeatFloorJson m = Data.GetFloor(_selectBeat, _selectBeatNote);
            if (m != null)
            {
                m.floorId = EditorGUILayout.IntField("ID:", m.floorId);

                m.heightlevel = EditorGUILayout.IntField("Height:", m.heightlevel);
                if(m.heightlevel != _chasmHeight)
                {
                    if (m.heightlevel > heightRange.y)
                    {
                        m.heightlevel = (int)heightRange.y;
                    }
                    else if (m.heightlevel < heightRange.x)
                    {
                        m.heightlevel = (int)heightRange.x;
                    }
                }
                else
                {
                    m.heightlevel = _chasmHeight;
                }

                EditorGUILayout.BeginHorizontal();

                
                _copyLen = EditorGUILayout.IntField("len:", _copyLen);

                if (GUILayout.Button("Copy"))
                {
                    MusicBeatJson currBeat = Data.GetMusicBeatJson(_selectBeat);
                    int currCopyIndex = 1;
                    int currNoteIndex = _selectBeatNote + 1;
                    int currBeatIndex = _selectBeat;
                    while(currCopyIndex <= _copyLen && (currBeat != null))
                    {
                        for (int index = currNoteIndex; index < currBeat.split; index++)
                        {
                            MusicBeatFloorJson currFloor = Data.GetFloor(currBeatIndex, index);
                            currFloor.CopyFrom(m);

                            currCopyIndex++;
                            if(currCopyIndex > _copyLen)
                            {
                                break;
                            }
                        }

                        currBeatIndex++;
                        currNoteIndex = 0;
                        currBeat = Data.GetMusicBeatJson(currBeatIndex);
                    }
                    
                }
                EditorGUILayout.EndHorizontal();

            }
        }
        EditorGUILayout.EndVertical();
    }

    void ShowEnvArea()
    {
        EditorGUILayout.BeginVertical("box");
        {
            EditorGUILayout.LabelField("添加触发器", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            {
                //_editorOp.trigger = EditorGUILayout.IntField(_editorOp.trigger);
                _editorOp.envIndenty = (MusicBeatEnvIdentityType)EditorGUILayout.EnumPopup("Env Identity", (MusicBeatEnvIdentityType)_editorOp.envIndenty);

                if (GUILayout.Button("+"))
                {
                    Data.AddEnv(_selectBeat, _selectBeatNote, (int)_editorOp.envIndenty);
                    _editorOp.envIndenty =  MusicBeatEnvIdentityType.Type_BossAppear;
                    GUI.FocusControl("");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("触发器", EditorStyles.boldLabel);

            MusicBeatEnvJson m = Data.GetEnv(_selectBeat, _selectBeatNote);
            if (m != null && !m.IsEmpty())
            {

                for (int i = 0; i < m.list.Count; ++i)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        int t = m.list[i];
                        if (t == (int)MusicBeatEnvIdentityType.Type_BossAppear)
                        {
                            m.userData = EditorGUILayout.IntField("Boss ID:", (int)m.userData);
                           // m.userData = _userData;
                        }
                        else if(t == (int)MusicBeatEnvIdentityType.Type_BossDisappera)
                        {
                            m.userData = EditorGUILayout.IntField("Boss ID:",(int)m.userData);
                            //m.userData = _userData;
                        }
                        else if(t == (int)MusicBeatEnvIdentityType.Type_SpeedScale)
                        {
                            m.userData = EditorGUILayout.FloatField("SpeedScale:", m.userData);
                           // m.userData = _userData;
                        }   
                        else if(t == (int)MusicBeatEnvIdentityType.Type_UserGuide)
                        {
                            //_userData = EditorGUILayout.FloatField("UserGuide", _userData);
                            EditorGUILayout.BeginVertical();
                            _editorOp.userGuideType = (UserGuideType)EditorGUILayout.EnumPopup("UserGuide", (UserGuideType)m.userData);
                            m.userNote = EditorGUILayout.TextField("note：", m.userNote);
                            m.userData = (float)_editorOp.userGuideType;
                            EditorGUILayout.EndVertical();
                        }

                        if (GUILayout.Button("-"))
                        {
                            Data.RemoveEnv(_selectBeat, _selectBeatNote, t);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

            }
        }
        EditorGUILayout.EndVertical();
    }
    #endregion

    void Update()
    {
        if (!string.IsNullOrEmpty(curMusicFilePath))
        {
            _progress = Data.Proportion;

            UpdatePlayingBeat();
        }

    }

    private bool _bAltDown = false;
    private bool _bCtrlDown = false;
    void UserInput()
    {
        Event e = Event.current;
        if (e == null)
            return;

        if (e.isKey)
        {
            if (e.keyCode == KeyCode.LeftAlt || e.keyCode == KeyCode.RightAlt)
            {
                _bAltDown = (e.type == EventType.keyDown);
                return;
            }

            EBeatLine currLine = Data.Lines[_selectLine];

            if (e.type == EventType.KeyDown && !string.IsNullOrEmpty(curMusicFilePath))
            {
                if(e.keyCode == KeyCode.Keypad1)
                {
                    _editorOp.bossNoteIndentityTypeTpl = MusicBeatNoteIdentityType_Boss.Type_Group1;
                   
                }
                else if(e.keyCode == KeyCode.Keypad2)
                {
                    _editorOp.bossNoteIndentityTypeTpl = MusicBeatNoteIdentityType_Boss.Type_Group2;
                }
                else if (
                    (currLine.EventType == EnumLineType.UpNote)
                    ||(currLine.EventType == EnumLineType.DownNote)
                    )
                {
                    EnumBeatItemType eType = EnumBeatItemType.eNone;
                    EnumBeatItemTypeEx exType = EnumBeatItemTypeEx.eNone;

                    if (e.keyCode == KeyCode.Space)
                    {
                        PressKeySpace();
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.G)
                    {
                        Data.MusicForward();
                    }
                    else if (e.keyCode == KeyCode.F)
                    {
                        Data.MusicBack();
                    }
                    else if(e.keyCode == KeyCode.Q)
                    {
                        Debug.Log("Q");
                        eType = EnumBeatItemType.eNormaNote;
                    }
                    else if (e.keyCode == KeyCode.W)
                    {
                        Debug.Log("W");
                        eType = EnumBeatItemType.eReverseNote;
                    }
                    else if (e.keyCode == KeyCode.E)
                    {
                        Debug.Log("E");
                        eType = EnumBeatItemType.eHoldNote;
                        exType = EnumBeatItemTypeEx.eHoldBegin;
                    }
                    else if (e.keyCode == KeyCode.R)
                    {
                        Debug.Log("右");
                        eType = EnumBeatItemType.eHoldNote;
                        exType = EnumBeatItemTypeEx.eHoldEnd;
                    }
                    else if (e.keyCode == KeyCode.A)
                    {
                        Debug.Log("A");
                        eType = EnumBeatItemType.eBossNote;
                    }
                    else if (e.keyCode == KeyCode.S)
                    {
                        Debug.Log("S");
                        eType = EnumBeatItemType.eLaunchNote;
                        exType = EnumBeatItemTypeEx.eHoldBegin;
                    }
                    else if(e.keyCode == KeyCode.D)
                    {
                        Debug.Log("D");
                        eType = EnumBeatItemType.eLaunchNote;
                        exType = EnumBeatItemTypeEx.eHoldEnd;
                    }
                    else if (e.keyCode == KeyCode.Z)
                    {
                        Debug.Log("Z");
                        eType = EnumBeatItemType.eBossComboNote;
                        exType = EnumBeatItemTypeEx.eHoldBegin;
                    }
                    else if (e.keyCode == KeyCode.X)
                    {
                        Debug.Log("X");
                        eType = EnumBeatItemType.eBossComboNote;
                        exType = EnumBeatItemTypeEx.eHoldEnd;
                    }              
                    else if (e.keyCode == KeyCode.Delete)
                    {
                        Debug.Log("删除");
                        e.Use();
                        Data.RemoveNote(_selectBeat, _selectBeatNote, currLine.EventType);
                    }
                    else if (e.keyCode == KeyCode.Y)
                    {
                        e.Use();

                        MusicBeatNoteJson noteJson = Data.GetNote(_selectBeat, _selectBeatNote, currLine.EventType);
                        if (noteJson != null)
                        {
                            int yeah = noteJson.yeah == 1 ? 0 : 1;
                            Data.SetNote(_selectBeat, _selectBeatNote, noteJson.type, noteJson.exType, yeah, currLine.EventType);
                            if(noteJson.type == (int)EnumBeatItemType.eBossNote)
                            {
                                noteJson.noteIdentity = (int)_editorOp.bossNoteIndentityTypeTpl;
                            }     
                            Debug.Log(string.Format("Beat[{0}]Note[{1}] SetYeah[{2}]", _selectBeat, _selectBeatNote, yeah));
                        }
                        else
                        {
                            Debug.Log("操作无效: 没有选中音符点");
                        }
                    }

                    if (eType != EnumBeatItemType.eNone)
                    {
                        e.Use();
                        int yeah = Data.GetNoteYeah(_selectBeat, _selectBeatNote, currLine.EventType);
                        MusicBeatNoteJson noteJson = Data.SetNote(_selectBeat, _selectBeatNote, (int)eType, (int)exType, yeah, currLine.EventType);
                        if(noteJson.type == (int)EnumBeatItemType.eBossNote)
                        {
                            noteJson.noteIdentity = (int)_editorOp.bossNoteIndentityTypeTpl;
                        }   
                   }


                }
                else if (currLine.EventType == EnumLineType.Obstacle)
                {
                    if (e.keyCode == KeyCode.T)
                    {
                        Debug.Log("T");
                        Data.AddObstacle(_selectBeat, _selectBeatNote, 1);
                    }
                }
            }
            
        }
    }

    void PressKeySpace()
    {
        PlayOrPause();
    }

    bool Cut()
    {
        if (Copy())
        {
            int s = _editorOp.copyRangeStart;
            int e = _editorOp.copyRangeEnd;
            for (int i = s; i <= e; ++i)
            {
                Data.RemoveNote(i, EnumLineType.UpNote);
                Data.RemoveNote(i, EnumLineType.DownNote);
            }
            return true;
        }

        return false;
    }

    bool Copy()
    {
        _editorOp.copyBufferUp = new System.Collections.Generic.List<MusicBeatNoteJson>();
        _editorOp.copyBufferDown = new System.Collections.Generic.List<MusicBeatNoteJson>();

        int s = _editorOp.copyRangeStart;
        int e = _editorOp.copyRangeEnd;
        if (Data.CheckBeatRangeInvalid(s, e))
        {    
            for (int i = s; i <= e; ++i)
            {
                _editorOp.copyBufferUp.AddRange(Data.GetNote(i, EnumLineType.UpNote));
                _editorOp.copyBufferDown.AddRange(Data.GetNote(i, EnumLineType.DownNote));
            }

            return true;
        }

        return false;
    }

    bool Delete()
    {
        int s = _editorOp.copyRangeStart;
        int e = _editorOp.copyRangeEnd;
        if (Data.CheckBeatRangeInvalid(s, e))
        {
            for (int i = s; i <= e; i++)
            {
                Data.RemoveNote(i, EnumLineType.UpNote);
                Data.RemoveNote(i, EnumLineType.DownNote);
            }
            return true;
        }

        return false;
    }

    bool Paste()
    {

        int s = _editorOp.copyRangeStart;
        int e = _editorOp.copyRangeEnd;
        int d = e - s;
        int t = _editorOp.copyToBeat;

        if (!Data.CheckBeatRangeInvalid(s, e))
            return false;
        if (!Data.CheckBeatRangeInvalid(t, t))
            return false;

        for (int i = s; i <= e; i++)
        {
            int beatIdx = t + (i - s);
            if (Data.CheckBeatRangeInvalid(beatIdx, beatIdx))
            {
                Data.SplitBeat(beatIdx, beatIdx, Data.GetMusicBeatJson(i).split);
            }
        }

        if ((_editorOp.copyBufferUp != null && _editorOp.copyBufferUp.Count > 0))
        {
            foreach (MusicBeatNoteJson noteJson in _editorOp.copyBufferUp)
            {
                int beatIdx = t + (noteJson.beatIndex - s);
                if (Data.CheckBeatRangeInvalid(beatIdx, beatIdx))
                {
                    MusicBeatNoteJson target = Data.SetNote(beatIdx, noteJson.noteIndex, noteJson.type, noteJson.exType, noteJson.yeah, EnumLineType.UpNote);
                    if(noteJson.type == (int)EnumBeatItemType.eBossNote)
                    {
                        target.noteIdentity = noteJson.noteIdentity;
                    }     
                    
                }
            }
        }

        if ((_editorOp.copyBufferDown != null && _editorOp.copyBufferDown.Count > 0))
        {
            foreach (MusicBeatNoteJson noteJson in _editorOp.copyBufferDown)
            {
                int beatIdx = t + (noteJson.beatIndex - s);
                if (Data.CheckBeatRangeInvalid(beatIdx, beatIdx))
                {
                    MusicBeatNoteJson target = Data.SetNote(beatIdx, noteJson.noteIndex, noteJson.type, noteJson.exType, noteJson.yeah, EnumLineType.DownNote);
                    if(noteJson.type == (int)EnumBeatItemType.eBossNote)
                    {
                        target.noteIdentity = noteJson.noteIdentity;
                    }                       
                }
            }
        }


        return true;

    }


    string Report(bool progress)
    {
        string dir = Application.dataPath;
        dir += "/Resources/MusicChart";
        string[] filesAry = Directory.GetFiles(dir, "*.txt");
        string ret = 
            "谱面" + "\t" +
            "歌曲时间" + "\t" +
            "谱面时间" + "\t" +
            "音乐偏移" + "\t" +
            "BPM" + "\t" +
            "diff" + "\t" + "\t" +
            "音符总数" + "\t" +
         
            "普通Note" + "\t" +
            "反向Note" + "\t" +
            "BossNote" + "\t" +
            "连击Note" + "\t" +
            "长按Note" + "\t" ;

        for(int i=0; i< filesAry.Length; ++i)
        {
            string path = filesAry[i];
            string name = Path.GetFileNameWithoutExtension(path);

            ret += "\n";
            ret += ReportChart(path);

            if (progress)
            {
                EditorUtility.DisplayProgressBar("导出统计信息", name, (i+1f)/filesAry.Length);
            }
        }

        return ret;
    }

    string ReportChart(string path)
    {
        int comboPerBeatInHold = Data._comboPerBeatInHold;

        string chartName = Path.GetFileNameWithoutExtension(path);

        FileInfo fInfo = new FileInfo(path);
        if (!fInfo.Exists)
        {
            return chartName + "\t路径不存在:" + path;
        }

        string content = File.ReadAllText(path);
        MusicBeatChartJson json = JsonUtility.FromJson<MusicBeatChartJson>(content);
        if (json == null)
        {
            return chartName + "\t加载失败:" + path;
        }
        if (!json.Check(comboPerBeatInHold, ref content))
        {
            content = content.Replace("\n", "\t");
            return chartName + "\t检查失败:" + content;
        }

        string musicLength = "读取音乐时间失败";
        {
            string clipName = Data.GetMusicClipName(json.musicId);
            AudioClip clip = Resources.Load<AudioClip>(MusicBeatAudioSource.PathBGM + clipName);
            if (clip != null)
            {
                musicLength = clip.length.ToString();
            }
        }

        string chartLength = json.GetChartLength().ToString();
        string offset = string.Format("{0:F3}", json.musicOffset);
        string bpm = json.GetBpm().ToString();
        string diff = json.GetDiff().ToString();

        string noteData = "上轨音符统计:\t" + ReportNoteData(json, comboPerBeatInHold, EnumLineType.UpNote);
        noteData += "\n";
        noteData += "\t\t\t\t\t\t" + "下轨音符统计:\t" + ReportNoteData(json, comboPerBeatInHold, EnumLineType.DownNote);


        return chartName + "\t" +
               musicLength + "\t" +
               chartLength + "\t" +
               offset + "\t" +
               bpm + "\t" +
               diff + "\t" +
               noteData + "\t";
    }


    string ReportNoteData(MusicBeatChartJson json, int comboPerBeatInHold, EnumLineType noteType)
    {
        string result = string.Empty;
        List<MusicBeatNoteJson> notes = null;
        if (noteType == EnumLineType.UpNote)
        {
            notes = json.upNotes;
        }
        else if (noteType == EnumLineType.DownNote)
        {
            notes = json.downNotes;
        }


        //string combo = json.GetComboCount(comboPerBeatInHold, noteType).ToString();
        string noteCount = notes.Count.ToString();
        string eNoraml = json.GetCountByType(EnumBeatItemType.eNormaNote, noteType).ToString();
        string eReverse = json.GetCountByType(EnumBeatItemType.eReverseNote, noteType).ToString();
        string eBoss = json.GetCountByType(EnumBeatItemType.eBossNote, noteType).ToString();
        string eCombo = json.GetCountByType(EnumBeatItemType.eLaunchNote, noteType).ToString();
        string eHold = json.GetCountByType(EnumBeatItemType.eHoldNote, noteType).ToString();

        result += noteCount + "\t" + eNoraml + "\t" + eReverse + "\t" + eBoss + "\t" + eCombo + "\t" + eHold + "\t";

        return result;
    }
}
