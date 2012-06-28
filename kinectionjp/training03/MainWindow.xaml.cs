using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace training03
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 制御するKinectセンサー
        /// </summary>
        KinectSensor kinect = null;

        /// <summary>
        /// Kinectセンサーからの画像情報を受けとるバッファ
        /// </summary>
        private byte[] pixelBuffer = null;

        /// <summary>
        /// 画面に表示するビットマップ
        /// </summary>
        private WriteableBitmap bmpBuffer = null;

        /// <summary>
        /// コンストラクター
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded( object sender, RoutedEventArgs e )
        {
            try {
                // 利用可能なKinectを探す
                kinect = KinectSensor.KinectSensors.First( k =>
                {
                    return k.Status == KinectStatus.Connected;
                } );

                // 利用可能なKinectがなかった
                if ( kinect == null ) {
                    throw new Exception( "Kinectが接続されていないか、準備ができていません" );
                }

                // カラーストリームの有効化
                kinect.ColorStream.Enable( ColorImageFormat.RgbResolution640x480Fps30 );

                // バッファの初期化
                pixelBuffer = new byte[kinect.ColorStream.FramePixelDataLength];
                bmpBuffer = new WriteableBitmap( kinect.ColorStream.FrameWidth,
                    kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null );

                rgbImage.Source = bmpBuffer;

                // イベントハンドラの登録
                kinect.ColorFrameReady +=
                    new EventHandler<ColorImageFrameReadyEventArgs>( kinect_ColorFrameReady );

                // Kinectセンサーからのストリーム取得を開始
                kinect.Start();
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
            }
        }

        /// <summary>
        /// カラーストリームのデータ更新イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void kinect_ColorFrameReady( object sender, ColorImageFrameReadyEventArgs e )
        {
            try {
                // 更新データを取得する
                using ( ColorImageFrame imageFrame = e.OpenColorImageFrame() ) {
                    if ( imageFrame != null ) {
                        // 画像情報をバッファにコピー
                        imageFrame.CopyPixelDataTo( pixelBuffer );

                        // ビットマップに描画
                        Int32Rect src = new Int32Rect( 0, 0, imageFrame.Width, imageFrame.Height );
                        bmpBuffer.WritePixels( src, pixelBuffer, imageFrame.Width * 4, 0 );
                    }
                }
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
            }
        }

        /// <summary>
        /// 終了処理(ウィンドウが閉じられるとき)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e )
        {
            try {
                // Kinectを動かしていたら止める
                if ( kinect != null ) {
                    // イベントハンドラの削除
                    kinect.ColorFrameReady -= kinect_ColorFrameReady;

                    // ストリームの停止とリソースの解放
                    kinect.Stop();
                    kinect.Dispose();

                    kinect = null;
                }
            }
            catch ( Exception ex ) {
                MessageBox.Show( ex.Message );
            }
        }
    }
}
