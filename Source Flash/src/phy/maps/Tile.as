package phy.maps
{
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.display.BlendMode;
   import flash.geom.Matrix;
   import flash.geom.Point;
   import flash.geom.Rectangle;

   public class Tile extends Bitmap
   {


      private var _digable:Boolean;

      public function Tile(param1:BitmapData, param2:Boolean)
      {
         super(param1);
         this._digable = param2;
      }

      public function Dig(param1:Point, param2:Bitmap, param3:Bitmap = null) : void
      {
         var _loc4_:BitmapData = null;
         var _loc5_:Matrix = new Matrix(1,0,0,1,param1.x,param1.y);
         if(Boolean(param2) && this._digable)
         {
            _loc5_.tx -= param2.width / 2;
            _loc5_.ty -= param2.height / 2;
            bitmapData.draw(param2,_loc5_,null,BlendMode.ERASE);
         }
         if(Boolean(param3) && this._digable)
         {
            // Keep the crater brink only where the ground is still there. This used to be
            // brink.clone().draw(this, m, null, BlendMode.ALPHA), which Ruffle gets wrong: its ALPHA
            // blend leaves pixels unchanged where the ground was erased, so the brink's filled centre
            // was pasted over the hole. copyPixels with an alpha source multiplies the alphas the same way.
            _loc4_ = new BitmapData(param3.bitmapData.width,param3.bitmapData.height,true,0);
            _loc4_.copyPixels(param3.bitmapData,param3.bitmapData.rect,new Point(0,0),bitmapData,new Point(param1.x - param3.width / 2,param1.y - param3.height / 2),true);
            _loc5_.tx = param1.x - param3.width / 2;
            _loc5_.ty = param1.y - param3.height / 2;
            bitmapData.draw(_loc4_,_loc5_,null,param3.blendMode);
            _loc4_.dispose();
         }
      }

      public function DigFillRect(param1:Rectangle) : void
      {
         bitmapData.fillRect(param1,0);
      }

      public function GetAlpha(param1:int, param2:int) : uint
      {
         return bitmapData.getPixel32(param1,param2) >> 24 & 0xFF;
      }

      public function dispose() : void
      {
         bitmapData.dispose();
      }
   }
}
