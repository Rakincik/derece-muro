import React from 'react';

interface TooltipProps {
    children: React.ReactNode;
    content: React.ReactNode;
    position?: 'top' | 'bottom' | 'left' | 'right';
    className?: string;
}

export function Tooltip({ children, content, position = 'top', className = '' }: TooltipProps) {
    if (!content) return <>{children}</>;

    const pos = {
        top: 'bottom-full mb-2 left-1/2 -translate-x-1/2',
        bottom: 'top-full mt-2 left-1/2 -translate-x-1/2',
        left: 'right-full mr-2 top-1/2 -translate-y-1/2',
        right: 'left-full ml-2 top-1/2 -translate-y-1/2',
    };

    const arrow = {
        top: 'top-full left-1/2 -translate-x-1/2 -mt-[1px]',
        bottom: 'bottom-full left-1/2 -translate-x-1/2 -mb-[1px] rotate-180',
        left: 'left-full top-1/2 -translate-y-1/2 -ml-[1px] -rotate-90',
        right: 'right-full top-1/2 -translate-y-1/2 -mr-[1px] rotate-90',
    };

    return (
        <div className={`relative group/tooltip inline-flex ${className}`}>
            {children}
            <div className={`absolute z-[100] ${pos[position]} opacity-0 scale-95 pointer-events-none group-hover/tooltip:opacity-100 group-hover/tooltip:scale-100 transition-all duration-200 ease-out`}>
                <div className="bg-[#0A1931] text-white text-[11px] font-bold px-3 py-1.5 rounded-lg shadow-xl shadow-[#0A1931]/20 whitespace-nowrap relative">
                    {content}
                    <svg className={`absolute text-[#0A1931] h-2 w-full left-0 ${arrow[position]}`} x="0px" y="0px" viewBox="0 0 255 255" xmlSpace="preserve">
                        <polygon className="fill-current" points="0,0 127.5,127.5 255,0"/>
                    </svg>
                </div>
            </div>
        </div>
    );
}
