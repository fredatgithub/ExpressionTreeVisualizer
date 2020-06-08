﻿using System.Diagnostics;
using ExpressionTreeVisualizer.Serialization;
using Microsoft.VisualStudio.DebuggerVisualizers;
using PostSharp.Community.Packer;

[assembly: Packer]

[assembly: DebuggerVisualizer(
    visualizer: typeof(ExpressionTreeVisualizer.Visualizer),
    visualizerObjectSource: typeof(ExpressionTreeVisualizer.VisualizerDataObjectSource),
    Target = typeof(System.Linq.Expressions.Expression),
    Description = "Expression Tree Visualizer")]

[assembly: DebuggerVisualizer(
    visualizer: typeof(ExpressionTreeVisualizer.Visualizer),
    visualizerObjectSource: typeof(ExpressionTreeVisualizer.VisualizerDataObjectSource),
    Target = typeof(System.Linq.Expressions.ElementInit),
    Description = "Expression Tree Visualizer")]

[assembly: DebuggerVisualizer(
    visualizer: typeof(ExpressionTreeVisualizer.Visualizer),
    visualizerObjectSource: typeof(ExpressionTreeVisualizer.VisualizerDataObjectSource),
    Target = typeof(System.Linq.Expressions.MemberBinding),
    Description = "Expression Tree Visualizer")]

[assembly: DebuggerVisualizer(
    visualizer: typeof(ExpressionTreeVisualizer.Visualizer),
    visualizerObjectSource: typeof(ExpressionTreeVisualizer.VisualizerDataObjectSource),
    Target = typeof(System.Linq.Expressions.SwitchCase),
    Description = "Expression Tree Visualizer")]

[assembly: DebuggerVisualizer(
    visualizer: typeof(ExpressionTreeVisualizer.Visualizer),
    visualizerObjectSource: typeof(ExpressionTreeVisualizer.VisualizerDataObjectSource),
    Target = typeof(System.Linq.Expressions.CatchBlock),
    Description = "Expression Tree Visualizer")]

[assembly: DebuggerVisualizer(
    visualizer: typeof(ExpressionTreeVisualizer.Visualizer),
    visualizerObjectSource: typeof(ExpressionTreeVisualizer.VisualizerDataObjectSource),
    Target = typeof(System.Linq.Expressions.LabelTarget),
    Description = "Expression Tree Visualizer")]

namespace ExpressionTreeVisualizer {
    public abstract class VisualizerWindowBase : Periscope.VisualizerWindowBase<VisualizerWindow, Config> { }

    public abstract class VisualizerBase : DialogDebuggerVisualizer { }

    public class Visualizer : VisualizerBase {
        protected override void Show(IDialogVisualizerService windowService, IVisualizerObjectProvider objectProvider) =>
            Periscope.Visualizer.Show<VisualizerWindow, Config>(GetType(), objectProvider, new Periscope.GithubProjectInfo("zspitz","expressiontreevisualizer"));
    }
}
