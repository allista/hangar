/*  
Script: RectanglePacker.js  
    An algorithm implementation in JavaScript for rectangle packing.  
  
Author:  
    IvГЎn Montes <drslump@drslump.biz>, <http://blog.netxus.es>  
  
License:  
    LGPL - Lesser General Public License  
  
Credits:  
    - Algorithm based on <http://www.blackpawn.com/texts/lightmaps/default.html>  
*/   
   
/*  
    Class: NETXUS.RectanglePacker  
    A class that finds an 'efficient' position for a rectangle inside another rectangle  
    without overlapping the space already taken.  
      
    Algorithm based on <http://www.blackpawn.com/texts/lightmaps/default.html>  
      
    It uses a binary tree to partition the space of the parent rectangle and allocate the   
    passed rectangles by dividing the partitions into filled and empty.  
*/   
   
   
// Create a NETXUS namespace object if it doesn't exists   
if (typeof NETXUS === 'undefined')   
    var NETXUS = function() {};        
       
   
/*    
    Constructor: NETXUS.RectanglePacker  
    Initializes the object with the given maximum dimensions  
      
    Parameters:  
      
        width - The containing rectangle maximum width as integer  
        height - The containing rectangle maximum height as integer  
          
*/     
NETXUS.RectanglePacker = function ( width, height ) {   
       
    this.root = {};   
   
    // initialize   
    this.reset( width, height );       
}   
   
   
/*  
    Resets the object to its initial state by initializing the internal variables  
  
    Parameters:  
      
        width - The containing rectangle maximum width as integer  
        height - The containing rectangle maximum height as integer  
*/   
NETXUS.RectanglePacker.prototype.reset = function ( width, height ) {   
    this.root.x = 0;   
    this.root.y = 0;   
    this.root.w = width;   
    this.root.h = height;   
    delete this.root.lft;   
    delete this.root.rgt;   
       
    this.usedWidth = 0;   
    this.usedHeight = 0;       
}   
       
   
/*  
    Returns the actual used dimensions of the containing rectangle.  
      
    Returns:  
      
        A object composed of the properties: 'w' for width and 'h' for height.   
*/   
NETXUS.RectanglePacker.prototype.getDimensions = function () {   
    return { w: this.usedWidth, h: this.usedHeight };      
}   
       
       
/*  
    Finds a suitable place for the given rectangle  
      
    Parameters:  
      
        w - The rectangle width as integer.  
        h - The rectangle height as integer.  
          
    Returns:  
      
        If there is room for the rectangle then returns the coordinates as an object   
        composed of 'x' and 'y' properties.   
        If it doesn't fit returns null  
*/         
NETXUS.RectanglePacker.prototype.findCoords = function ( w, h ) {   
       
    // private function to traverse the node tree by recursion   
    function recursiveFindCoords ( node, w, h ) {   
   
        // private function to clone a node coords and size   
        function cloneNode ( node ) {   
            return {   
                x: node.x,   
                y: node.y,   
                w: node.w,   
                h: node.h      
            };   
        }          
           
        // if we are not at a leaf then go deeper   
        if ( node.lft ) {   
            // check first the left branch if not found then go by the right   
            var coords = recursiveFindCoords( node.lft, w, h );   
            return coords ? coords : recursiveFindCoords( node.rgt, w, h );    
        }   
        else   
        {   
            // if already used or it's too big then return   
            if ( node.used || w > node.w || h > node.h )   
                return null;   
                   
            // if it fits perfectly then use this gap   
            if ( w == node.w && h == node.h ) {   
                node.used = true;   
                return { x: node.x, y: node.y };   
            }   
               
            // initialize the left and right leafs by clonning the current one   
            node.lft = cloneNode( node );   
            node.rgt = cloneNode( node );   
               
            // checks if we partition in vertical or horizontal   
            if ( node.w - w > node.h - h ) {   
                node.lft.w = w;   
                node.rgt.x = node.x + w;   
                node.rgt.w = node.w - w;       
            } else {   
                node.lft.h = h;   
                node.rgt.y = node.y + h;   
                node.rgt.h = node.h - h;                               
            }   
               
            return recursiveFindCoords( node.lft, w, h );          
        }   
    }   
           
    // perform the search   
    var coords = recursiveFindCoords( this.root, w, h );   
    // if fitted then recalculate the used dimensions   
    if (coords) {   
        if ( this.usedWidth < coords.x + w )   
            this.usedWidth = coords.x + w;   
        if ( this.usedHeight < coords.y + h )   
            this.usedHeight = coords.y + h;   
    }   
    return coords;   
}


function doRender( params ) {
		
	// sort functions
	var sorting = {
		'none'	: function (a,b) { return  0 },
		'width'	: function (a,b) { return a.w - b.w },
		'height': function (a,b) { return a.h - b.h },
		'area'  : function (a,b) { return a.w*a.h - b.w*b.h },
		'magic' : function (a,b) { return Math.max(a.w,a.h) - Math.max(b.w,b.h) }
	}		
	
	// create the random sized blocks
	var blocks = []
	for (var i=0; i<params.blocks; i++)
		blocks[i] = { 
			w: params.minWidth + Math.round( (params.maxWidth-params.minWidth) * Math.random() ),
			h: params.minHeight + Math.round( (params.maxHeight-params.minHeight) * Math.random() )
		};
	
	// perform the selected sort algorithm and reverse the result if needed	
	blocks.sort( sorting[ params.sort ] );		
	if (params.reverse)
		blocks.reverse();
	
	// create the Rectangle Packer object
	var packer = new NETXUS.RectanglePacker( params.canvasWidth, params.canvasHeight );
	
	var coords;
	// process all the blocks
	for (var i=0; i<blocks.length; i++) {
		// obtain the coordinates for the current block
		coords = packer.findCoords( blocks[i].w, blocks[i].h );
		if (coords) {
			blocks[i].x = coords.x;
			blocks[i].y = coords.y;
		} else {
			blocks[i].noFit = true;
		} 			
	}
	
	
	var el,
		blkEl,
		liEl,
		noFitEl = document.getElementById('nofit'),
		canvasEl = document.getElementById('canvas');
	
	// remove the current blocks from the canvas element
	while (canvasEl.hasChildNodes())
		canvasEl.removeChild(canvasEl.firstChild);
		
	// configure the canvas element
	canvasEl.style.position = 'relative';
	canvasEl.style.width = params.canvasWidth + 'px';
	canvasEl.style.height = params.canvasHeight + 'px';
	
	
	var colors = [ 
		'#B02B2C', '#d15600', '#c79810', '#73880a', '#6bba70', '#3f4c6b', '#356aa0', '#d01f3c' 
	];
	
	var totalArea = 0,
		fitted = 0,
		// IE messes the width and height adding the borders into the calculations
		// we use this flag to adapt the calculations
		notIE = /*@cc_on!@*/true;
		
	for (var i=0; i<blocks.length; i++) {
		
		// check if the block was rejected
		if (blocks[i].noFit) {
			// add the dimensions to the non fitted list
			liEl = document.createElement('LI');
			liEl.appendChild( document.createTextNode( blocks[i].w + 'x' + blocks[i].h ) );
			noFitEl.appendChild(liEl);
			
			continue;
		}
		
		// count this block as succesfully included 
		fitted++;
		
		// sum up this block area to the total area for statistics
		totalArea += blocks[i].w * blocks[i].h;
		
		// create the new block and style it
		blkEl = document.createElement('DIV');
		blkEl.style.border = '1px solid white';
		blkEl.style.background = colors[ i % colors.length ];
		blkEl.style.position = 'absolute';
		blkEl.style.top = blocks[i].y + 'px';
		blkEl.style.left = blocks[i].x + 'px';
		blkEl.style.width = (blocks[i].w - 2 * parseInt(blkEl.style.borderWidth) * notIE) + 'px';
		blkEl.style.height = (blocks[i].h - 2 * parseInt(blkEl.style.borderWidth) * notIE) + 'px';
		
		canvasEl.appendChild( blkEl );
	}
	
	// Calculate the used dimensions
	var dim = packer.getDimensions();		
	el = document.getElementById('status-size');
	while (el.hasChildNodes())
		el.removeChild(el.firstChild);
	el.appendChild( document.createTextNode( dim.w + 'x' + dim.h ) );
	
	// Calculate the space allocation efficiency
	el = document.getElementById('status-efficiency');
	while (el.hasChildNodes())
		el.removeChild(el.firstChild);
	el.appendChild( document.createTextNode( ((totalArea*100) / (dim.w*dim.h)).toFixed(2) ) );
	
	// Calculate the percentage of blocks successfully included
	el = document.getElementById('status-fitted');
	while (el.hasChildNodes())
		el.removeChild(el.firstChild);
	el.appendChild( document.createTextNode( ((fitted*100) / blocks.length).toFixed(0) ) );
}


function refreshCanvas() {
	
	var params = {};
	
	var canvasSize = document.getElementById('canvasSize').options[
		document.getElementById('canvasSize').selectedIndex 
	].value.split('x');
	
	params.canvasWidth = canvasSize[0];
	params.canvasHeight = canvasSize[1];
	
	params.blocks = parseInt(document.getElementById('blocksNo').value);
	
	params.minWidth = parseInt(document.getElementById('minWidth').value);
	params.maxWidth = parseInt(document.getElementById('maxWidth').value);

	params.minHeight = parseInt(document.getElementById('minHeight').value);
	params.maxHeight = parseInt(document.getElementById('maxHeight').value);
	
	params.sort = document.getElementById('sort').options[ 
		document.getElementById('sort').selectedIndex 
	].value;
	
	params.reverse = document.getElementById('reverse').checked;
	
	var nofit = document.getElementById('nofit');
	while (nofit.hasChildNodes())
		nofit.removeChild(nofit.firstChild);
	
	doRender( params )	
}
