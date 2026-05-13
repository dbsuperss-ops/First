using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SoccerlineApp.Models;

public class Comment
{
    public string Nickname { get; set; } = string.Empty;
    public string UserID { get; set; } = string.Empty;
    public string AuthorIp { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

public class Post : INotifyPropertyChanged
{
    public string BoardName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorIp { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string Views { get; set; } = string.Empty;
    public string Likes { get; set; } = string.Empty;
    public string Dislikes { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<Comment> Comments { get; set; } = new();

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    private bool _detailsFetched = false;
    public bool DetailsFetched
    {
        get => _detailsFetched;
        set { if (_detailsFetched != value) { _detailsFetched = value; OnPropertyChanged(); } }
    }

    private bool _isArchived = false;
    public bool IsArchived
    {
        get => _isArchived;
        set { if (_isArchived != value) { _isArchived = value; OnPropertyChanged(); } }
    }

    private bool _isDeleted = false;
    public bool IsDeleted
    {
        get => _isDeleted;
        set { if (_isDeleted != value) { _isDeleted = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
