﻿using CocoJumper.Base.EventModels;
using CocoJumper.Base.Events;
using CocoJumper.Controls;
using CocoJumper.Events;
using CocoJumper.Models;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CocoJumper.CodeMarkerTag
{
    internal class CodeMarkerTagger : ITagger<IntraTextAdornmentTag>
    {
        private readonly ITextView _textView;
        private readonly ITextBuffer _buffer;
        private readonly Dictionary<int, ITagSpan<IntraTextAdornmentTag>> _taggers = new Dictionary<int, ITagSpan<IntraTextAdornmentTag>>();
        private readonly MarkerViewModel _searchMarkerViewModel;
        private ITagSpan<IntraTextAdornmentTag> _searcher;

        public CodeMarkerTagger(ITextView textView, ITextBuffer buffer, IEventAggregator eventAggregator)
        {
            _textView = textView;
            _buffer = buffer;
            eventAggregator.AddListener(new DelegateListener<SearchResultEvent>(OnSearch), true);
            eventAggregator.AddListener(new DelegateListener<StartNewSearchEvent>(OnNewSearch), true);
            eventAggregator.AddListener(new DelegateListener<ExitEvent>(OnExit), true);
            _searchMarkerViewModel = new MarkerViewModel();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return _taggers.Values;
        }

        private void OnExit(ExitEvent e)
        {
            _taggers.Clear();
            _searcher = null;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                new SnapshotSpan(_buffer.CurrentSnapshot, new Span(0, _buffer.CurrentSnapshot.Length - 1))));
        }

        private void OnSearch(SearchResultEvent e)
        {
            double lineHeight = (_textView as IWpfTextView)?.LineHeight ?? 0;

            foreach (SearchEvent eSearchEvent in e.SearchEvents)
            {
                if (!_taggers.ContainsKey(eSearchEvent.StartPosition))
                {
                    _taggers.Add(eSearchEvent.StartPosition,
                        new TagSpan<IntraTextAdornmentTag>(
                            span: new SnapshotSpan(_buffer.CurrentSnapshot, new Span(eSearchEvent.StartPosition, 0)),
                            tag: new IntraTextAdornmentTag(new LetterWithMarker(eSearchEvent.Letters, lineHeight), null,
                                PositionAffinity.Predecessor)
                        )
                    );
                }
            }

            List<int> keysToRemove = _taggers.Keys.Where(p => p != 0 && !e.SearchEvents.Exists(x => x.StartPosition == p)).ToList();
            foreach (int i in keysToRemove)
            {
                _taggers.Remove(i);
            }

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                new SnapshotSpan(_buffer.CurrentSnapshot, new Span(0, _buffer.CurrentSnapshot.Length - 1))));
        }

        private void OnNewSearch(StartNewSearchEvent e)
        {
            _searchMarkerViewModel.Update(e.Text, _textView.LineHeight, e.MatchNumber);
            if (_searcher != null)
                return;

            _searcher =
                new TagSpan<IntraTextAdornmentTag>(
                    span: new SnapshotSpan(_buffer.CurrentSnapshot, new Span(e.StartPosition, 0)),
                    tag: new IntraTextAdornmentTag(new SearcherWithMarker(_searchMarkerViewModel), null, PositionAffinity.Predecessor)
                );
            _taggers.Add(0, _searcher);

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                new SnapshotSpan(_buffer.CurrentSnapshot, new Span(0, _buffer.CurrentSnapshot.Length - 1))));
        }
    }
}