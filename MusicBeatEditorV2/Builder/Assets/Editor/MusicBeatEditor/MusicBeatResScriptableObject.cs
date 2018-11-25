﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MusicBeatResScriptableObject : ScriptableObject
{
    public int _comboPerBeatInHold = 1;
    public GUISkin _skin;
    public Texture2D _clearTexture;
    public Texture2D _yeahTexture;
    public List<Texture2D> _noteTextures = new List<Texture2D>();
    public List<Texture2D> _noteEndTextures = new List<Texture2D>();
    public List<AudioClip> _NoteAudioClips = new List<AudioClip>();
    public List<AudioClip> _NoteEndAudioClips = new List<AudioClip>();

    public Texture2D _bossNoteNormal;
    public Texture2D _bossNoteReverse;

}
