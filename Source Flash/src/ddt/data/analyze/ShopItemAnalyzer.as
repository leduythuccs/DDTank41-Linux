package ddt.data.analyze
{
   import com.pickgliss.loader.DataAnalyzer;
   import com.pickgliss.utils.ObjectUtils;
   import ddt.data.goods.ShopItemInfo;
   import flash.events.Event;
   import flash.events.TimerEvent;
   import flash.utils.Timer;
   import flash.utils.getTimer;
   import road7th.data.DictionaryData;
   
   public class ShopItemAnalyzer extends DataAnalyzer
   {
      
      public var shopinfolist:DictionaryData;
      
      private var _xml:XML;
      
      private var _shoplist:XMLList;
      
      private var index:int = -1;
      
      private var _timer:Timer;
      
      public function ShopItemAnalyzer(param1:Function)
      {
         super(param1);
      }
      
      override public function analyze(param1:*) : void
      {
         this._xml = new XML(param1);
         if(this._xml.@value == "true")
         {
            this.shopinfolist = new DictionaryData();
            this._shoplist = this._xml.Store..Item;
            this.parseShop(null);
         }
         else
         {
            message = this._xml.@message;
            onAnalyzeError();
         }
      }
      
      private function parseShop(param1:Event) : void
      {
         this._timer = new Timer(1);
         this._timer.addEventListener(TimerEvent.TIMER,this.__partexceute);
         this._timer.start();
      }
      
      private function __partexceute(param1:TimerEvent) : void
      {
         var _loc2_:ShopItemInfo = null;
         var _loc3_:int = 0;
         // Patched: the original handled 40 items per 30 ms tick (about 5.6 s for the 7500 item
         // templates whatever the machine); work for up to 25 ms per tick instead.
         var __sliceStart:int = getTimer();
         while(getTimer() - __sliceStart < 25)
         {
            ++this.index;
            if(this.index >= this._shoplist.length())
            {
               this._timer.removeEventListener(TimerEvent.TIMER,this.__partexceute);
               this._timer.stop();
               this._timer = null;
               onAnalyzeComplete();
               return;
            }
            _loc2_ = new ShopItemInfo(int(this._shoplist[this.index].@ID),int(this._shoplist[this.index].@TemplateID));
            ObjectUtils.copyPorpertiesByXML(_loc2_,this._shoplist[this.index]);
            this.shopinfolist.add(_loc2_.GoodsID,_loc2_);
            _loc3_++;
         }
      }
   }
}

