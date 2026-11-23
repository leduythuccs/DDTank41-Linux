package ddt.data.analyze
{
   import com.pickgliss.loader.DataAnalyzer;
   import com.pickgliss.utils.ObjectUtils;
   import ddt.data.goods.ItemTemplateInfo;
   import flash.events.TimerEvent;
   import flash.utils.Timer;
   import flash.utils.getTimer;
   import road7th.data.DictionaryData;
   
   public class ItemTempleteAnalyzer extends DataAnalyzer
   {
      
      public var list:DictionaryData = new DictionaryData();
      
      private var _xml:XML;
      
      private var _timer:Timer;
      
      private var _xmllist:XMLList;
      
      private var _index:int;
      
      public function ItemTempleteAnalyzer(param1:Function)
      {
         super(param1);
      }
      
      override public function analyze(param1:*) : void
      {
         this._xml = new XML(param1);
         this.list = new DictionaryData();
         this.parseTemplate();
      }
      
      protected function parseTemplate() : void
      {
         if(this._xml.@value == "true")
         {
            this._xmllist = this._xml.ItemTemplate..Item;
            this._index = -1;
            this._timer = new Timer(1);
            this._timer.addEventListener(TimerEvent.TIMER,this.__partexceute);
            this._timer.start();
         }
         else
         {
            message = this._xml.@message;
            onAnalyzeError();
         }
      }
      
      private function __partexceute(param1:TimerEvent) : void
      {
         var _loc2_:ItemTemplateInfo = null;
         var _loc3_:int = 0;
         // Patched: the original handled 40 items per 30 ms tick (about 5.6 s for the 7500 item
         // templates whatever the machine); work for up to 25 ms per tick instead.
         var __sliceStart:int = getTimer();
         while(getTimer() - __sliceStart < 25)
         {
            ++this._index;
            if(this._index >= this._xmllist.length())
            {
               this._timer.removeEventListener(TimerEvent.TIMER,this.__partexceute);
               this._timer.stop();
               this._timer = null;
               onAnalyzeComplete();
               return;
            }
            _loc2_ = new ItemTemplateInfo();
            ObjectUtils.copyPorpertiesByXML(_loc2_,this._xmllist[this._index]);
            this.list.add(_loc2_.TemplateID,_loc2_);
            _loc3_++;
         }
      }
   }
}

