﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using static ExpressionTreeToString.Globals;
using ExpressionTreeVisualizer.Serialization.Util;
using System.Linq.Expressions;
using ExpressionTreeToString.Util;
using System.Reflection;
using static ExpressionTreeVisualizer.Serialization.EndNodeTypes;
using static ExpressionTreeToString.Util.Functions;
using System.Collections;
using static ExpressionTreeToString.FormatterNames;
using System.Runtime.CompilerServices;

namespace ExpressionTreeVisualizer.Serialization.ViewModels {
    [Serializable]
    [DebuggerDisplay("{FullPath}")]
    [Obsolete("Use ExpressionTreeVisualizer.Serializaion.ExpressionNodeData or ExpressionTreeVisualizer.UI.ExpressionNodeDataViewModel")]
    public class ExpressionNodeData : INotifyPropertyChanged {
        public List<ExpressionNodeData> Children { get; set; }
        public string NodeType { get; set; } // ideally this should be an intersection type of multiple enums
        public string? ReflectionTypeName { get; set; }
        public (int start, int length) Span { get; set; }
        public int SpanEnd => Span.start + Span.length;
        public string? StringValue { get; set; }
        public string? Name { get; set; }
        public string? Closure { get; set; }
        public EndNodeTypes? EndNodeType { get; set; }
        public bool IsDeclaration { get; set; }
        public string PathFromParent { get; set; } = "";
        public string FullPath { get; set; } = "";
        public (string @namespace, string typename, string propertyname)? ParentProperty { get; set; }
        public List<(string @namespace, string enumTypename, string membername)>? NodeTypesParts { get; set; }

        private readonly List<(string @namespace, string typename)>? _baseTypes;
        public List<(string @namespace, string typename)>? BaseTypes => _baseTypes;
        public string? WatchExpressionFormatString { get; set; }
        public bool EnableValueInNewWindow { get; set; }

        private readonly string[] _factoryMethodNames;
        public string[] FactoryMethodNames => _factoryMethodNames;

        public EndNodeData EndNodeData => new EndNodeData {
            Closure = Closure,
            Name = Name,
            Type = ReflectionTypeName,
            Value = StringValue
        };

        private static readonly HashSet<Type> propertyTypes = NodeTypes.SelectMany(x => new[] { x, typeof(IEnumerable<>).MakeGenericType(x) }).ToHashSet();

        internal ExpressionNodeData(object o, (string aggregatePath, string pathFromParent) path, VisualizerData visualizerData, bool isParameterDeclaration = false, PropertyInfo? pi = null, string? parentWatchExpression = null) :
            this(
                o, path, 
                visualizerData.Options.Language, visualizerData.CollectedEndNodes, visualizerData.PathSpans,
                isParameterDeclaration, pi, parentWatchExpression
            ) { }

        internal ExpressionNodeData(
            object o, (string aggregatePath, string pathFromParent) path, 
            string language, List<ExpressionNodeData> endNodes, Dictionary<string, (int start, int length)> pathSpans,
            bool isParameterDeclaration = false, PropertyInfo? pi = null, string? parentWatchExpression = null
        ) {
            var (aggregatePath, pathFromParent) = path;
            PathFromParent = pathFromParent;
            if (aggregatePath.IsNullOrWhitespace() || pathFromParent.IsNullOrWhitespace()) {
                FullPath = aggregatePath + pathFromParent;
            } else {
                FullPath = $"{aggregatePath}.{pathFromParent}";
            }

            switch (o) {
                case Expression expr:
                    NodeType = expr.NodeType.ToString();
                    NodeTypesParts = new List<(string @namespace, string enumTypename, string membername)> {
                        (typeof(ExpressionType).Namespace!, nameof(ExpressionType), NodeType)
                    };
                    if (expr is GotoExpression gexpr) {
                        NodeTypesParts.Add(
                            typeof(GotoExpressionKind).Namespace!, nameof(GotoExpressionKind), gexpr.Kind.ToString()
                        );
                    }
                    ReflectionTypeName = expr.Type.FriendlyName(language);
                    IsDeclaration = isParameterDeclaration;

                    // fill the Name and Closure properties, for expressions
                    Name = expr.Name(language);

                    if (expr is MemberExpression mexpr &&
                        mexpr.Expression?.Type is Type expressionType &&
                        expressionType.IsClosureClass()) {
                        Closure = expressionType.Name;
                    }

                    object? value = null;

                    // fill StringValue and EndNodeType properties, for expressions
                    switch (expr) {
                        case ConstantExpression cexpr when !cexpr.Type.IsClosureClass():
                            value = cexpr.Value;
                            EndNodeType = Constant;
                            break;
                        case ParameterExpression pexpr1:
                            EndNodeType = Parameter;
                            break;
                        case Expression e1 when expr.IsClosedVariable():
                            value = expr.ExtractValue();
                            EndNodeType = ClosedVar;
                            break;
                        case DefaultExpression defexpr:
                            value = defexpr.ExtractValue();
                            EndNodeType = Default;
                            break;
                    }
                    if (EndNodeType != null) { endNodes.Add(this); }

                    if (value != null) {
                        StringValue = StringValue(value, language);
                        EnableValueInNewWindow = value.GetType().InheritsFromOrImplementsAny(NodeTypes);
                    }

                    break;
                case MemberBinding mbind:
                    NodeType = mbind.BindingType.ToString();
                    NodeTypesParts = new List<(string @namespace, string enumTypename, string membername)> {
                        (typeof(MemberBindingType).Namespace!, nameof(MemberBindingType), NodeType)
                    };
                    Name = mbind.Member.Name;
                    break;
                case CallSiteBinder callSiteBinder:
                    NodeType = callSiteBinder.BinderType();
                    break;
                default:
                    NodeType = o.GetType().FriendlyName(language);
                    break;
            }

            if (pathSpans.TryGetValue(FullPath, out var span)) {
                Span = span;
            }

            if (parentWatchExpression.IsNullOrWhitespace()) {
                WatchExpressionFormatString = "{0}";
            } else if (pi is { }) {
                if (pi.DeclaringType is null) { throw new ArgumentNullException("pi.DeclaringType"); }
                
                var watchPathFromParent = PathFromParent;
                if (language == CSharp) {
                    WatchExpressionFormatString = $"(({pi.DeclaringType.FullName}){parentWatchExpression}).{watchPathFromParent}";
                } else {  //VisualBasic
                    watchPathFromParent = watchPathFromParent.Replace("[", "(").Replace("]", ")");
                    WatchExpressionFormatString = $"CType({parentWatchExpression}, {pi.DeclaringType.FullName}).{watchPathFromParent}";
                }
            }

            // populate Children
            var type = o.GetType();
            var preferredOrder = PreferredPropertyOrders.FirstOrDefault(x => x.type.IsAssignableFrom(type)).Item2;
            Children = type.GetProperties()
                .Where(prp =>
                    !(prp.DeclaringType!.Name == "BlockExpression" && prp.Name == "Result") &&
                    propertyTypes.Any(x => x.IsAssignableFrom(prp.PropertyType))
                )
                .OrderBy(prp => {
                    if (preferredOrder == null) { return -1; }
                    return Array.IndexOf(preferredOrder, prp.Name);
                })
                .ThenBy(prp => prp.Name)
                .SelectMany(prp => {
                    if (prp.PropertyType.InheritsFromOrImplements<IEnumerable>()) {
                        return (prp.GetValue(o) as IEnumerable).Cast<object>().Select((x, index) => ($"{prp.Name}[{index}]", x, prp));
                    } else {
                        return new[] { (prp.Name, prp.GetValue(o)!, prp) };
                    }
                })
                .Where(x => x.x != null)
                .SelectT((relativePath, o1, prp) => new ExpressionNodeData(
                    o1, (FullPath ?? "", relativePath), 
                    language, endNodes, pathSpans,
                    false, prp, WatchExpressionFormatString))
                .ToList();

            // populate URLs
            if (pi != null) {
                ParentProperty = (pi.DeclaringType.Namespace!, pi.DeclaringType.Name, pi.Name);
            }

            if (!baseTypes.TryGetValue(o.GetType(), out _baseTypes)) {
                _baseTypes = o.GetType().BaseTypes(true, true).Where(x => x != typeof(object) && x.IsPublic).Select(x => (x.Namespace!, x.Name)).Distinct().ToList();
                baseTypes[o.GetType()] = _baseTypes;
            }

            string? factoryMethodName = null;
            if (o is BinaryExpression || o is UnaryExpression) {
                BinaryUnaryMethods.TryGetValue(((Expression)o).NodeType, out factoryMethodName);
            }
            if (factoryMethodName.IsNullOrWhitespace()) {
                var publicType = o.GetType().BaseTypes(false, true).FirstOrDefault(x => !x.IsInterface && x.IsPublic);
                factoryMethods.TryGetValue(publicType, out _factoryMethodNames!);
            } else {
                _factoryMethodNames = new[] { factoryMethodName };
            }
        }

        private static readonly Dictionary<Type, List<(string @namespace, string typename)>> baseTypes = new Dictionary<Type, List<(string @namespace, string typename)>>();

        private static readonly Dictionary<Type, string[]> factoryMethods = typeof(Expression).GetMethods()
            .Where(x => x.IsStatic)
            .GroupBy(x => x.ReturnType, x => x.Name)
            .ToDictionary(x => x.Key, x => x.Distinct().Ordered().ToArray());


        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set => Extensions.NotifyChanged(this, ref _isSelected, value, args => PropertyChanged?.Invoke(this, args));
        }

        public void ClearSelection() {
            IsSelected = false;
            Children.ForEach(x => x.ClearSelection());
        }
    }
}
