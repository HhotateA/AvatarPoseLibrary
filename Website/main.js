const gitURL = 'https://api.github.com/repos/HhotateA/AvatarPoseLibrary'

window.addEventListener('DOMContentLoaded', () => {
    // 自動スクロールを実装し、無限ループを実現
    const carousel = document.querySelector('.carousel');
    const track = document.querySelector('.carousel-track');
    let speed = 0.5; // スクロール速度
    let scrollPosition = 0;
  
    function step() {
      scrollPosition += speed;
      if (scrollPosition >= track.scrollWidth / 2) {
        scrollPosition -= track.scrollWidth / 2;
      }
      carousel.scrollLeft = scrollPosition;
      requestAnimationFrame(step);
    }
  
    requestAnimationFrame(step);
    carousel.addEventListener('mouseenter', () => speed = 0);
    carousel.addEventListener('mouseleave', () => speed = 0.5);
    
    // UnityPackageリンクの作成
    const btn = document.querySelector('.unity-download');
    fetch(`${gitURL}/releases/latest`)
      .then(res => {
        if (!res.ok) throw new Error('failed to fetch release');
        return res.json();
      })
      .then(data => {
        // .unitypackage ファイルを探す
        const asset = data.assets.find(a => a.name.endsWith('.unitypackage'));
        if (asset) {
          btn.href = asset.browser_download_url;
          // オプションで aria-label や title も更新
          btn.title = `Download ${asset.name}`;
        }
      })
      .catch(err => {
        console.error('最新リリースの取得に失敗しました:', err);
      });
  });