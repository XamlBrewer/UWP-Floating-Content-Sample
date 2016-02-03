using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace XamlBrewer.Uwp.Controls
{
    /// <summary>
    /// A Content Control that can be dragged around.
    /// </summary>
    [TemplatePart(Name = BorderPartName, Type = typeof(Border))]
    [TemplatePart(Name = OverlayPartName, Type = typeof(UIElement))]
    public class FloatingContent : ContentControl
    {
        private const string BorderPartName = "PART_Border";
        private const string OverlayPartName = "PART_Overlay";

        private Border border;
        private UIElement overlay;

        public static readonly DependencyProperty BoundaryProperty =
            DependencyProperty.Register(
                "Boundary",
                typeof(FloatingBoundary),
                typeof(FloatingContent),
                new PropertyMetadata(FloatingBoundary.None));

        /// <summary>
        /// Initializes a new instance of the <see cref="FloatingContent"/> class.
        /// </summary>
        public FloatingContent()
        {
            this.DefaultStyleKey = typeof(FloatingContent);
        }

        public FloatingBoundary Boundary
        {
            get { return (FloatingBoundary)GetValue(BoundaryProperty); }
            set { SetValue(BoundaryProperty, value); }
        }

        /// <summary>
        /// Invoked whenever application code or internal processes (such as a rebuilding layout pass) call ApplyTemplate.
        /// In simplest terms, this means the method is called just before a UI element displays in your app.
        /// Override this method to influence the default post-template logic of a class.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            // Border
            this.border = this.GetTemplateChild(BorderPartName) as Border;
            if (this.border != null)
            {
                this.border.ManipulationStarted += Border_ManipulationStarted;
                this.border.ManipulationDelta += Border_ManipulationDelta;
                this.border.ManipulationCompleted += Border_ManipulationCompleted;

                // Move Canvas properties from control to border.
                Canvas.SetLeft(this.border, Canvas.GetLeft(this));
                Canvas.SetLeft(this, 0);
                Canvas.SetTop(this.border, Canvas.GetTop(this));
                Canvas.SetTop(this, 0);

                // Move Margin to border.
                this.border.Padding = this.Margin;
                this.Margin = new Thickness(0);
            }
            else
            {
                // Exception
                throw new Exception("Floating Control Style has no Border.");
            }

            // Overlay
            this.overlay = GetTemplateChild(OverlayPartName) as UIElement;

            this.Loaded += Floating_Loaded;
        }
        private void Border_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            if (this.overlay != null)
            {
                var ani = new DoubleAnimation()
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromSeconds(1.5))
                };
                var storyBoard = new Storyboard();
                storyBoard.Children.Add(ani);
                Storyboard.SetTarget(ani, overlay);
                ani.SetValue(Storyboard.TargetPropertyProperty, "Opacity");
                storyBoard.Begin();
            }
        }

        private void Border_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (this.overlay != null)
            {
                var ani = new DoubleAnimation()
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.25))
                };
                var storyBoard = new Storyboard();
                storyBoard.Children.Add(ani);
                Storyboard.SetTarget(ani, overlay);
                ani.SetValue(Storyboard.TargetPropertyProperty, "Opacity");
                storyBoard.Begin();
            }
        }

        private void Floating_Loaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement el = GetClosestParentWithSize(this);
            if (el == null)
            {
                return;
            }

            el.SizeChanged += Floating_SizeChanged;
        }

        private void Floating_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var left = Canvas.GetLeft(this.border);
            var top = Canvas.GetTop(this.border);

            Rect rect = new Rect(left, top, this.border.ActualWidth, this.border.ActualHeight);

            AdjustCanvasPosition(rect);
        }

        private void Border_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var left = Canvas.GetLeft(this.border) + e.Delta.Translation.X;
            var top = Canvas.GetTop(this.border) + e.Delta.Translation.Y;

            Rect rect = new Rect(left, top, this.border.ActualWidth, this.border.ActualHeight);
            var moved = AdjustCanvasPosition(rect);
            if (!moved)
            {
                // We hit the boundary. Stop the inertia.
                e.Complete();
            }
        }

        /// <summary>
        /// Adjusts the canvas position according to the IsBoundBy* properties.
        /// </summary>
        private bool AdjustCanvasPosition(Rect rect)
        {
            // Free floating.
            if (this.Boundary == FloatingBoundary.None)
            {
                Canvas.SetLeft(this.border, rect.Left);
                Canvas.SetTop(this.border, rect.Top);

                return true;
            }

            FrameworkElement el = GetClosestParentWithSize(this);

            // No parent
            if (el == null)
            {
                // We probably never get here.
                return false;
            }

            var position = new Point(rect.Left, rect.Top); ;

            if (this.Boundary == FloatingBoundary.Parent)
            {
                Rect parentRect = new Rect(0, 0, el.ActualWidth, el.ActualHeight);
                position = AdjustedPosition(rect, parentRect);
            }

            if (this.Boundary == FloatingBoundary.Window)
            {
                var ttv = el.TransformToVisual(Window.Current.Content);
                var topLeft = ttv.TransformPoint(new Point(0, 0));
                Rect parentRect = new Rect(topLeft.X, topLeft.Y, Window.Current.Bounds.Width - topLeft.X, Window.Current.Bounds.Height - topLeft.Y);
                position = AdjustedPosition(rect, parentRect);
            }

            // Set new position
            Canvas.SetLeft(this.border, position.X);
            Canvas.SetTop(this.border, position.Y);

            return position == new Point(rect.Left, rect.Top);
        }

        /// <summary>
        /// Returns the adjusted the topleft position of a rectangle so that is stays within a parent rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="parentRect">The parent rectangle.</param>
        /// <returns></returns>
        private Point AdjustedPosition(Rect rect, Rect parentRect)
        {
            var left = rect.Left;
            var top = rect.Top;

            if (left < -parentRect.Left)
            {
                // Left boundary hit.
                left = -parentRect.Left;
            }
            else if (left + rect.Width > parentRect.Width)
            {
                // Right boundary hit.
                left = parentRect.Width - rect.Width;
            }

            if (top < -parentRect.Top)
            {
                // Top hit.
                top = -parentRect.Top;
            }
            else if (top + rect.Height > parentRect.Height)
            {
                // Bottom hit.
                top = parentRect.Height - rect.Height;
            }

            return new Point(left, top);
        }

        /// <summary>
        /// Gets the closest parent with a real size.
        /// </summary>
        private FrameworkElement GetClosestParentWithSize(FrameworkElement element)
        {
            while (element != null && (element.ActualHeight == 0 || element.ActualWidth == 0))
            {
                // Crawl up the Visual Tree.
                element = element.Parent as FrameworkElement;
            }

            return element;
        }
    }
}
